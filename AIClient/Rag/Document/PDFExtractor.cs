using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Windows;
using Microsoft.Extensions.Logging;
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
        foreach (var page in Document.GetPages())
        {
            var words = page.GetWords(NearestNeighbourWordExtractor.Instance).ToArray();
            if (words.Any() && padding != null)
            {
                var box = page.CropBox.Bounds;
                var thickness = padding.Value;
                thickness = new Thickness(
                    box.Left + thickness.Left,
                    box.Top - thickness.Top,
                    box.Right - thickness.Right,
                    box.Bottom + thickness.Bottom);
                words = words.Where(word =>
                {
                    var boundingBox = word.BoundingBox;
                    return boundingBox.Bottom < thickness.Top && // 单词的下边缘要低于页眉线
                           boundingBox.Top > thickness.Bottom && // 单词的上边缘要高于页脚线
                           boundingBox.Left > thickness.Left && // 单词的左边缘要在页边距右侧; 
                           boundingBox.Right < thickness.Right; // 单词的右边缘要在页边距左侧
                }).ToArray();
            }

            var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);
            var pageNumber = page.Number;
            progress?.Report(pageNumber);
            _cache.TryAdd(pageNumber, new PageCacheItem(page, pageNumber, blocks));
        }

        var orderDetector = new UnsupervisedReadingOrderDetector(10);
        foreach (var pageCacheItem in _cache.Values)
        {
            progress?.Report(pageCacheItem.PageNumber);
            var orderedBlocks = orderDetector.Get(pageCacheItem.TotalTextBlocks).ToArray();
            pageCacheItem.RemainingTextBlocks = orderedBlocks.ToList();
            pageCacheItem.TotalTextBlocks = orderedBlocks;
        }
    }

    /// <summary>
    /// 解析出内容树。
    /// </summary>
    /// <returns>内容树的根节点列表。</returns>
    private List<PDFContentNode> ExtractContentTree()
    {
        if (!Document.TryGetBookmarks(out var bookmarks, true))
        {
            // 如果没有书签，可以返回一个代表整个文档的单一节点
            // 或者直接返回空列表，表示无法按目录结构解析
            return new List<PDFContentNode>();
        }

        var rootNodes = new List<PDFContentNode>();
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
    public IList<PDFContentNode> Analyze()
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

    private Point GetTopLeft(PDFContentNode node)
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
    private PDFContentNode? BuildNodeTree(BookmarkNode bookmark)
    {
        // 尝试获取书签指向的页码
        // DestinationProvider可以更稳定地获取页码
        var node = new PDFContentNode(bookmark.Title, bookmark.Level);
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
            node.Destination = docNode.Destination;
            node.StartPage = docNode.PageNumber;
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
    /// 将树形结构扁平化为一个列表，方便后续处理。
    /// </summary>
    private static IEnumerable<PDFContentNode> Flatten(IEnumerable<PDFContentNode> nodes)
    {
        return nodes.SelectMany(n => new[] { n }.Concat(Flatten(n.Children)));
    }

    private class PageCacheItem
    {
        public PageCacheItem(Page page, int pageNumber, IReadOnlyList<TextBlock> totalTextBlocks)
        {
            Page = page;
            PageNumber = pageNumber;
            TotalTextBlocks = totalTextBlocks;
            RemainingTextBlocks = totalTextBlocks.ToList();
        }

        public int PageNumber { get; set; }

        public List<TextBlock> RemainingTextBlocks { get; set; }

        public IReadOnlyList<TextBlock> TotalTextBlocks { get; set; }

        public Page Page { get; }
    }

    /// <summary>
    /// 从指定的页码范围提取段落。
    /// </summary>
    private void ExtractParagraphs(PDFContentNode preNode,
        int processIndex, int endPage, Point? endTopLeft = null)
    {
        var startPage = preNode.StartPage;
        //对于实际的第一个扁平点，需要trim起始点之前的内容
        if (processIndex == 0)
        {
            var startPoint = preNode.StartPoint; // 获取起始点
            var remainingTextBlocks = _cache[startPage].RemainingTextBlocks;
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
        }

        for (int i = startPage; i <= endPage; i++)
        {
            var page = _cache[i];
            if (i < endPage || endTopLeft == null)
            {
                // 如果不是最后一页，或者没有指定终止点，则使用当前页面的所有文本块
                preNode.Paragraphs.Add(new PDFPageBlocks(page.RemainingTextBlocks.ToArray(), i));
                page.RemainingTextBlocks.Clear();
            }
            else
            {
                var remainingTextBlocks = page.RemainingTextBlocks;
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

                preNode.Paragraphs.Add(new PDFPageBlocks(blocks, i));
            }
        }
    }

    public void Dispose()
    {
        Document.Dispose();
    }
}

public static class PDFExtractorExtensions
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="nodes">
    ///     顶层节点
    ///     <para><b>警告：</b> 请勿传入已扁平化的节点列表，否则结果不正确。</para>
    /// </param>
    /// <param name="docId"></param>
    /// <param name="llmCall"></param>
    /// <param name="logger"></param>
    /// <param name="nodeProgress"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public static async Task<List<SKDocChunk>> ToSKDocChunks(this IEnumerable<PDFContentNode> nodes,
        string docId, Func<string, CancellationToken, Task<string>> llmCall, ILogger? logger = null,
        IProgress<PDFContentNode>? nodeProgress = null,
        CancellationToken token = default)
    {
        var docChunks = new List<SKDocChunk>();
        foreach (var contentNode in nodes)
        {
            await ApplyRaptor(docId, contentNode, docChunks, llmCall, logger, nodeProgress, token: token);
        }

        return docChunks;
    }

    private static async Task<SKDocChunk> ApplyRaptor(string docId, PDFContentNode node,
        List<SKDocChunk> chunks, Func<string, CancellationToken, Task<string>> llmCall, ILogger? logger = null,
        IProgress<PDFContentNode>? nodeProgress = null, string? parentId = null, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var nodeLevel = node.Level;
        var nodeChunk = new SKDocChunk()
        {
            Key = Guid.NewGuid().ToString(),
            DocumentId = docId,
            ParentKey = parentId ?? String.Empty,
            Level = nodeLevel,
            Title = node.Title,
        };
        if (node.HasChildren)
        {
            nodeChunk.HasChild = true;
            // 生成摘要：子节点标题 + 摘要
            var summaryBuilder = new StringBuilder(node.Title + "\nSubsections:");
            foreach (var child in node.Children)
            {
                var chunk = await ApplyRaptor(docId, child, chunks, llmCall, logger, nodeProgress, nodeChunk.Key,
                    token);
                summaryBuilder.AppendLine($"- {child.Title}");
                summaryBuilder.AppendLine(chunk.Summary);
            }

            var chunkText = await llmCall(summaryBuilder.ToString(), token);
            nodeChunk.Summary = chunkText;
        }
        else
        {
            var nodeContentBuilder = new StringBuilder();
            if (node.Paragraphs.Count > 0)
            {
                foreach (var paragraph in node.Paragraphs)
                {
                    var paragraphContent = paragraph.Content;
                    if (string.IsNullOrEmpty(paragraphContent.Trim()))
                    {
                        logger?.LogWarning("跳过空段落，所在页码：{PageNumber}", paragraph.PageNumber);
                        continue; // 跳过空段落
                    }

                    nodeContentBuilder.AppendLine(paragraphContent);
                    //todo：检查段落是否超过 Embedding限制（一般不会）
                    chunks.Add(new SKDocChunk()
                    {
                        Key = Guid.NewGuid().ToString(),
                        DocumentId = docId,
                        Text = paragraphContent,
                        Summary = await llmCall(paragraphContent, token),
                        Level = nodeLevel + 1,
                        ParentKey = nodeChunk.Key,
                        HasChild = false
                    });
                    nodeProgress?.Report(node);
                }

                nodeChunk.HasChild = true;
            }

            var nodeContent = nodeContentBuilder.ToString();
            if (string.IsNullOrEmpty(nodeContent.Trim()))
            {
                logger?.LogWarning("节点没有内容，所在页码：{StartPage}, 标题：{Title}", node.StartPage, node.Title);
                nodeChunk.HasChild = false;
                nodeChunk.Text = node.Title;
                nodeChunk.Summary = node.Title;
            }
            else
            {
                //存在段落
                nodeChunk.HasChild = false;
                nodeChunk.Summary = await llmCall(nodeContent, token);
                //存在子节点时，Text为空，表示需要进一步查找
            }
        }

        chunks.Add(nodeChunk);
        nodeProgress?.Report(node);
        return nodeChunk;
    }
}