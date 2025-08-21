using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using UglyToad.PdfPig.Outline;
using UglyToad.PdfPig.Outline.Destinations;

namespace LLMClient.Rag.Document;

public class PDFExtractor : IDisposable
{
    private readonly ConcurrentDictionary<int, PageCacheItem> _cache;

    public PdfDocument Document { get; }

    public PDFExtractor(string filePath)
    {
        Document = PdfDocument.Open(filePath);
        _cache = new ConcurrentDictionary<int, PageCacheItem>();
    }

    public void Initialize(IProgress<int>? progress = null, Thickness? padding = null)
    {
        _cache.Clear();
        var orderDetector = new UnsupervisedReadingOrderDetector(10);
        foreach (var page in Document.GetPages())
        {
            var box = page.CropBox.Bounds;
            Thickness? thickness;
            if (padding == null)
            {
                thickness = null;
            }
            else
            {
                var paddingValue = padding.Value;
                thickness = new Thickness(
                    box.Left + paddingValue.Left,
                    box.Top - paddingValue.Top,
                    box.Right - paddingValue.Right,
                    box.Bottom + paddingValue.Bottom);
            }

            var words = page.GetWords(NearestNeighbourWordExtractor.Instance).ToArray();
            if (words.Any() && thickness.HasValue)
            {
                var thicknessValue = thickness.Value;
                words = words.Where(word =>
                {
                    var boundingBox = word.BoundingBox;
                    return boundingBox.Bottom < thicknessValue.Top && // 单词的下边缘要低于页眉线
                           boundingBox.Top > thicknessValue.Bottom && // 单词的上边缘要高于页脚线
                           boundingBox.Left > thicknessValue.Left && // 单词的左边缘要在页边距右侧; 
                           boundingBox.Right < thicknessValue.Right; // 单词的右边缘要在页边距左侧
                }).ToArray();
            }

            var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);
            var pageNumber = page.Number;
            progress?.Report(pageNumber);
            var pdfImages = page.GetImages().ToArray();
            if (pdfImages.Any() && thickness.HasValue)
            {
                var thicknessValue = thickness.Value;
                pdfImages = pdfImages.Where(img =>
                {
                    var boundingBox = img.Bounds;
                    return boundingBox.Bottom < thicknessValue.Top && // 图片的下边缘要低于页眉线
                           boundingBox.Top > thicknessValue.Bottom && // 图片的上边缘要高于页脚线
                           boundingBox.Left > thicknessValue.Left && // 图片的左边缘要在页边距右侧; 
                           boundingBox.Right < thicknessValue.Right; // 图片的右边缘要在页边距左侧
                }).ToArray();
            }

            var orderedBlocks = orderDetector.Get(blocks).ToArray();
            _cache.TryAdd(pageNumber, new PageCacheItem(page, pageNumber, orderedBlocks, pdfImages));
        }
    }

    /// <summary>
    /// 解析出内容树。
    /// </summary>
    /// <returns>内容树的根节点列表。</returns>
    private List<PDFNode> ExtractContentTree()
    {
        if (!Document.TryGetBookmarks(out var bookmarks, true))
        {
            // 如果没有书签，可以返回一个代表整个文档的单一节点
            // 或者直接返回空列表，表示无法按目录结构解析
            return new List<PDFNode>();
        }

        var rootNodes = new List<PDFNode>();
        foreach (var bookmark in bookmarks.Roots)
        {
            var node = BuildNodeTree(bookmark);
            if (node != null)
                rootNodes.Add(node);
        }

        return rootNodes;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns>根节点列表</returns>
    public IList<PDFNode> Analyze()
    {
        // 1. 递归构建基础树结构，只包含标题、层级和起始页码
        var rootNodes = this.ExtractContentTree();
        // 2. 将扁平化的节点列表，用于确定每个章节的页码范围
        var flatNodeList = Flatten(rootNodes)
            .OrderBy(n => n.StartPage)
            .ToList();
        int index = 0;
        // 3. 提取每个节点的文本内容
        for (int i = 0; i < flatNodeList.Count; i++)
        {
            var currentNode = flatNodeList[i];
            if (currentNode.HasChildren)
            {
                continue;
            }

            // 确定本章节的结束页和终止位置
            int endPage;
            Point? nextTopLeft = null;
            var nextNode = flatNodeList.Skip(i + 1).FirstOrDefault();
            if (nextNode != null)
            {
                // 结束页是下一个章节的起始页（为了防止遗漏）
                endPage = nextNode.StartPage;
                nextTopLeft = nextNode.StartPoint;
            }
            else
            {
                // 如果是最后一个章节，则直到文档末尾
                endPage = Document.NumberOfPages;
            }

            // 提取并填充段落
            ExtractParagraphs(currentNode, index, endPage, nextTopLeft);
            index++;
        }

        return rootNodes;
    }

    private Point GetTopLeft(PDFNode node)
    {
        var destination = node.Destination;
        if (destination == null)
        {
            var boxBounds = _cache[node.StartPage].Page.MediaBox.Bounds;
            return new Point(boxBounds.Left, boxBounds.Top);
        }

        Point topLeft;
        var coords = destination.Coordinates;
        switch (destination.Type)
        {
            case ExplicitDestinationType.XyzCoordinates:
                // 如果有坐标，使用坐标的左上角作为终止位置
                if (coords.Top != null && coords.Left != null)
                {
                    topLeft = new Point((double)coords.Left,
                        (double)coords.Top);
                }

                break;
            case ExplicitDestinationType.FitHorizontally:
            case ExplicitDestinationType.FitBoundingBoxHorizontally:
                // 只指定Top，使用默认Left=0
                if (coords.Top.HasValue)
                {
                    topLeft = new Point(0, (double)coords.Top);
                }

                break;

            case ExplicitDestinationType.FitVertically:
            case ExplicitDestinationType.FitBoundingBoxVertically:
                // 只指定Left，使用默认Top=页面高度（假设从顶部开始）
                var pageItem = _cache[node.StartPage]; // 获取页面以获取MediaBox
                var defaultTop = pageItem.Page.MediaBox.Bounds.Height; // 或page.MediaBox.UpperLeftY
                if (coords.Left.HasValue)
                {
                    topLeft = new Point((double)coords.Left, defaultTop);
                }

                break;
            case ExplicitDestinationType.FitRectangle:
                // 使用矩形的左上角作为终止点
                if (coords.Left.HasValue && coords.Top.HasValue)
                {
                    topLeft = new Point((double)coords.Left, (double)coords.Top);
                }

                // 可选：如果需要矩形范围，可以进一步使用Right/Bottom划分块
                break;

            case ExplicitDestinationType.FitPage:
            case ExplicitDestinationType.FitBoundingBox:
            default:
                // 无坐标：假设页面左上角
                pageItem = _cache[node.StartPage]; // 获取页面以获取MediaBox
                var boxBounds = pageItem.Page.MediaBox.Bounds;
                topLeft = new Point(boxBounds.Left, boxBounds.Top);
                break;
        }

        return topLeft;
    }

    /// <summary>
    /// 递归地从PdfPig的BookmarkNode构建我们的ContentNode树。
    /// </summary>
    private PDFNode? BuildNodeTree(BookmarkNode bookmark)
    {
        // 尝试获取书签指向的页码
        // DestinationProvider可以更稳定地获取页码
        var bookmarkTitle = bookmark.Title;
        var node = new PDFNode(bookmarkTitle, bookmark.Level);
        var bookmarkNodes = bookmark.Children;
        var contentNodes = node.Children;
        foreach (var bookmarkNode in bookmarkNodes)
        {
            var contentNode = BuildNodeTree(bookmarkNode);
            if (contentNode != null)
            {
                contentNodes.Add(contentNode);
            }
        }

        if (bookmark is DocumentBookmarkNode docNode)
        {
            var nodeDestination = docNode.Destination;
            node.Destination = nodeDestination;
            node.StartPage = docNode.PageNumber;
            //判断是否需要创建隐式子节点（有些节点不但承担书签作用，还承担内容节点作用）
            if (contentNodes.Count > 0)
            {
                var nodeStartPoint = GetTopLeft(node);
                var firstChildNode = contentNodes[0];
                var childDestination = GetTopLeft(firstChildNode);
                if (!nodeStartPoint.Equals(childDestination))
                {
                    //起始点不同，说明当前节点既是书签节点，也是内容节点
                    //通过正则智能地创建子节点title，如果title是"1. Introduction"且子节点是"1.1 Background"，则创建子节点title为"1.0 Introduction"
                    var implicitTitle = bookmarkTitle;
                    if (HeadingParser.TryParse(implicitTitle, out string numbering, out string title, out var levels))
                    {
                        implicitTitle = $"{numbering}.0 {title}";
                    }

                    var implicitNode = new PDFNode(implicitTitle, bookmark.Level + 1)
                    {
                        Destination = nodeDestination,
                        StartPage = node.StartPage,
                        StartPoint = nodeStartPoint
                    };
                    contentNodes.Insert(0, implicitNode);
                }
            }
        }
        else if (bookmark is ContainerBookmarkNode)
        {
            //对于虚拟节点，起始页取第一个子节点的起始页
            var contentNode = contentNodes.FirstOrDefault();
            if (contentNode == null)
            {
                return null;
            }

            node.Destination = contentNode.Destination;
            node.StartPage = contentNode.StartPage;
        }
        else
        {
            Trace.TraceWarning("未知的书签节点类型。");
            return null;
        }

        node.StartPoint = GetTopLeft(node);
        return node;
    }

    /// <summary>
    /// 比较两个 Destination 是否相等（基于 PageNumber 和可选坐标）。
    /// </summary>
    private bool DestinationsEqual(ExplicitDestination dest1, ExplicitDestination dest2)
    {
        if (dest1.PageNumber != dest2.PageNumber)
            return false;
        var dest1Coordinates = dest1.Coordinates;
        var dest2Coordinates = dest2.Coordinates;


        return true;
    }

    /// <summary>
    /// 将树形结构扁平化为一个列表，方便后续处理。
    /// </summary>
    private static IEnumerable<PDFNode> Flatten(IEnumerable<PDFNode> nodes)
    {
        return nodes.SelectMany(n => new[] { n }.Concat(Flatten(n.Children)));
    }

    private class PageCacheItem
    {
        public PageCacheItem(Page page, int pageNumber, IList<TextBlock> totalTextBlocks,
            IList<IPdfImage> pdfImages)
        {
            Page = page;
            PageNumber = pageNumber;
            RemainingPdfImages = pdfImages.ToList();
            RemainingTextBlocks = totalTextBlocks.ToList();
        }

        public int PageNumber { get; set; }

        public List<TextBlock> RemainingTextBlocks { get; set; }

        public List<IPdfImage> RemainingPdfImages { get; }

        public Page Page { get; }
    }

    /// <summary>
    /// 从指定的页码范围提取段落。
    /// </summary>
    private void ExtractParagraphs(PDFNode preNode, int processIndex,
        int endPage, Point? endTopLeft = null)
    {
        var startPage = preNode.StartPage;
        //对于实际的第一个扁平点，需要trim起始点之前的内容
        if (processIndex == 0)
        {
            var startPoint = preNode.StartPoint; // 获取起始点
            var pageCacheItem = _cache[startPage];
            var remainingTextBlocks = pageCacheItem.RemainingTextBlocks;
            var startBlock = remainingTextBlocks.FirstOrDefault(block =>
            {
                var boundingBox = block.BoundingBox;
                return boundingBox.Bottom < startPoint.Y && boundingBox.Right > startPoint.X;
            });
            if (startBlock != null)
            {
                var indexOf = remainingTextBlocks.IndexOf(startBlock);
                if (indexOf > 0)
                {
                    remainingTextBlocks.RemoveRange(0, indexOf);
                }
            }

            var remainingPdfImages = pageCacheItem.RemainingPdfImages;
            var startImage = remainingPdfImages.FirstOrDefault(image =>
            {
                var boundingBox = image.Bounds;
                return boundingBox.Bottom < startPoint.Y && boundingBox.Right > startPoint.X;
            });
            if (startImage != null)
            {
                var indexOf = remainingPdfImages.IndexOf(startImage);
                if (indexOf > 0)
                {
                    remainingPdfImages.RemoveRange(0, indexOf);
                }
            }
        }

        for (int i = startPage; i <= endPage; i++)
        {
            var page = _cache[i];
            var remainingTextBlocks = page.RemainingTextBlocks;
            var remainingPdfImages = page.RemainingPdfImages;
            if (i < endPage || endTopLeft == null)
            {
                // 如果不是最后一页，或者没有指定终止点，则使用当前页面的所有文本块
                preNode.Pages.Add(new PDFPage(remainingTextBlocks.ToArray(),
                    i, remainingPdfImages.ToArray()));
                remainingTextBlocks.Clear();
                remainingPdfImages.Clear();
            }
            else
            {
                var firstMatch = remainingTextBlocks.FirstOrDefault((block =>
                {
                    var boundingBox = block.BoundingBox;
                    var boundingBoxTop = boundingBox.Top;
                    var boundingBoxLeft = boundingBox.Left;
                    return boundingBoxTop < endTopLeft.Value.Y && boundingBoxLeft > endTopLeft.Value.X;
                }));
                TextBlock[] blocks;
                if (firstMatch != null)
                {
                    var indexOf = remainingTextBlocks.IndexOf(firstMatch);
                    // 如果是最后一页，且有终止点，则只取到终止点之前的文本块
                    blocks = remainingTextBlocks.Take(indexOf).ToArray();
                    remainingTextBlocks.RemoveRange(0, indexOf);
                }
                else
                {
                    // 如果是最后一页，且有终止点，则只取到终止点之前的文本块
                    blocks = remainingTextBlocks.ToArray();
                    remainingTextBlocks.Clear();
                }

                IPdfImage[] images;
                var firstOrDefault = remainingPdfImages.FirstOrDefault(image =>
                {
                    var boundingBox = image.Bounds;
                    var boundingBoxTop = boundingBox.Top;
                    var boundingBoxLeft = boundingBox.Left;
                    return boundingBoxTop < endTopLeft.Value.Y && boundingBoxLeft > endTopLeft.Value.X;
                });
                if (firstOrDefault != null)
                {
                    var indexOf = remainingPdfImages.IndexOf(firstOrDefault);
                    images = remainingPdfImages.Take(indexOf).ToArray();
                    remainingPdfImages.RemoveRange(0, indexOf);
                }
                else
                {
                    images = remainingPdfImages.ToArray();
                    remainingPdfImages.Clear();
                }

                preNode.Pages.Add(new PDFPage(blocks, i, images));
            }
        }
    }

    public void Dispose()
    {
        Document.Dispose();
    }
}