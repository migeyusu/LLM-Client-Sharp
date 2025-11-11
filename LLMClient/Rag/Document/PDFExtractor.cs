using System.Collections.Concurrent;
using System.Diagnostics;
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

    public long PageCount => Document.NumberOfPages;

    public Page GetPage(int pageNumber) => Document.GetPage(pageNumber);

    private PdfDocument Document { get; }

    public PDFExtractor(string filePath)
    {
        Document = PdfDocument.Open(filePath);
        _cache = new ConcurrentDictionary<int, PageCacheItem>();
    }

    /*page segmenter 有一个非常严重的问题，就是文本块内行分隔符使用换行符还是空格？ 有些时候如目录、代码需使用换行符，有些如自然语言段必然是空格。
     PdfPig默认使用空格，综合考虑接受默认设置 */

    private static readonly IPageSegmenter PageSegmenter =
        new DocstrumBoundingBoxes(new DocstrumBoundingBoxes.DocstrumBoundingBoxesOptions());

    public PageCacheItem Deserialize(Page page, Thickness? padding = null)
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

        var textBlocks = PageSegmenter.GetBlocks(words);
        var orderedBlocks = _orderDetector.Get(textBlocks).ToArray();
        var pdfImages = page.GetImages().ToArray();
        if (pdfImages.Any() && thickness.HasValue)
        {
            var thicknessValue = thickness.Value;
            pdfImages = pdfImages.Where(img =>
            {
                var boundingBox = img.Bounds;
                // 计算图片边界框和页边距区域的交集
                var intersectionLeft = Math.Max(boundingBox.Left, thicknessValue.Left);
                var intersectionTop = Math.Min(boundingBox.Top, thicknessValue.Top);
                var intersectionRight = Math.Min(boundingBox.Right, thicknessValue.Right);
                var intersectionBottom = Math.Max(boundingBox.Bottom, thicknessValue.Bottom);

                // 检查是否有交集
                if (intersectionLeft >= intersectionRight || intersectionBottom >= intersectionTop)
                {
                    return false; // 无交集
                }

                return true;
                /*// 计算交集面积
                var intersectionArea = (intersectionRight - intersectionLeft) * (intersectionTop - intersectionBottom);

                // 计算并集面积 = 图片面积 + 页边距面积 - 交集面积
                var imgArea = boundingBox.Width * boundingBox.Height;
                var thicknessArea = (thicknessValue.Right - thicknessValue.Left) * (thicknessValue.Top - thicknessValue.Bottom);
                var unionArea = imgArea + thicknessArea - intersectionArea;

                // 计算IOU（交并比）
                var iou = intersectionArea / unionArea;

                return iou > 0; // IOU大于0表示有交集*/
            }).ToArray();
        }

        return new PageCacheItem(page, page.Number, orderedBlocks, pdfImages);
    }

    private readonly UnsupervisedReadingOrderDetector _orderDetector = new(10);

    public void Initialize(IProgress<int>? progress = null, Thickness? padding = null)
    {
        _cache.Clear();
        foreach (var page in Document.GetPages())
        {
            var pageCacheItem = Deserialize(page, padding);
            var pageNumber = page.Number;
            progress?.Report(pageNumber);
            _cache.TryAdd(pageNumber, pageCacheItem);
        }
    }

    /// <summary>
    /// 解析出bookmark树。
    /// <para>递归构建基础树结构，只包含标题、层级和起始页码</para>
    /// </summary>
    /// <returns>内容树的根节点列表。</returns>
    public List<PDFNode> ExtractTree()
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
    public IList<PDFNode> Analyze(IList<PDFNode>? rootNodes = null)
    {
        // 1. 递归构建基础树结构，只包含标题、层级和起始页码
        rootNodes ??= ExtractTree();
        // 2. 将扁平化的节点列表，用于确定每个章节的页码范围
        var flatNodeList = rootNodes.Flatten()
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

            currentNode.ContentUnits.Clear();
            // 提取并填充段落
            ExtractParagraphs(currentNode, index, endPage, nextTopLeft);
            index++;
        }

        return rootNodes;
    }

    private static Point GetTopLeft(PDFNode node, Page startPage)
    {
        var destination = node.Destination;
        var boxBounds = startPage.MediaBox.Bounds;
        if (destination == null)
        {
            return new Point(boxBounds.Left, boxBounds.Top);
        }

        Point topLeft;
        var coords = destination.Coordinates;
        switch (destination.Type)
        {
            case ExplicitDestinationType.XyzCoordinates:
                // 如果有坐标，使用坐标的左上角作为终止位置
                if (coords is { Top: not null, Left: not null })
                {
                    topLeft = new Point((double)coords.Left,
                        (double)coords.Top);
                }
                else if (coords.Top != null)
                {
                    topLeft = new Point(0, (double)coords.Top);
                }
                else
                {
                    Trace.TraceWarning($"{node.Title} XyzCoordinates缺少Top坐标，可能发生错误！。");
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
                var defaultTop = boxBounds.Height; // 或page.MediaBox.UpperLeftY
                if (coords.Left.HasValue)
                {
                    topLeft = new Point((double)coords.Left, defaultTop);
                }

                break;
            case ExplicitDestinationType.FitRectangle:
                // 使用矩形的左上角作为终止点
                if (coords is { Left: not null, Top: not null })
                {
                    topLeft = new Point((double)coords.Left, (double)coords.Top);
                }

                // 可选：如果需要矩形范围，可以进一步使用Right/Bottom划分块
                break;

            case ExplicitDestinationType.FitPage:
            case ExplicitDestinationType.FitBoundingBox:
            default:
                // 无坐标：假设页面左上角
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
            var startPage = docNode.PageNumber;
            node.StartPage = startPage;
            var startPoint = GetTopLeft(node, Document.GetPage(startPage));
            node.StartPoint = startPoint;
            //判断是否需要创建隐式子节点（有些节点不但承担书签作用，还承担内容节点作用）
            if (contentNodes.Count > 0)
            {
                var firstChildNode = contentNodes[0];
                var childStartPoint = firstChildNode.StartPoint;
                if (!startPoint.Equals(childStartPoint) || firstChildNode.StartPage != startPage)
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
                        StartPage = startPage,
                        StartPoint = startPoint
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
            node.StartPoint = contentNode.StartPoint;
        }
        else
        {
            Trace.TraceWarning("未知的书签节点类型。");
            return null;
        }


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

    public class PageCacheItem
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
            //按照Block级别分割存在Block聚类错误的问题，暂时接受
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
                preNode.ContentUnits.Add(new PDFPage(remainingTextBlocks.ToArray(),
                    i, remainingPdfImages.ToArray()));
                remainingTextBlocks.Clear();
                remainingPdfImages.Clear();
            }
            else
            {
                //兼容：假设出现双列布局，则终止点是右侧列的起始点
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

                preNode.ContentUnits.Add(new PDFPage(blocks, i, images));
            }
        }
    }

    public void Dispose()
    {
        Document.Dispose();
    }
}