using System.Collections.Concurrent;
using System.Text;
using System.Windows;
using Markdig.Extensions.JiraLinks;
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
    /// <summary>
    /// 表示PDF文档中的一个内容节点，可以对应一个目录章节。
    /// </summary>
    public class ContentNode
    {
        /// <summary>
        /// 章节标题 (来自书签)
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 在目录树中的层级
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// 该章节在PDF中的起始页码
        /// </summary>
        public int StartPage { get; set; }

        public Point StartPoint { get; set; }

        public ExplicitDestination? Destination { get; set; }

        /// <summary>
        /// 该章节的子节点 (子目录)
        /// </summary>
        public List<ContentNode> Children { get; set; } = new List<ContentNode>();

        public bool HasChildren => Children.Count > 0;

        /// <summary>
        /// 该节点直接包含的段落列表。只有叶子节点或需要存储自身内容的节点才填充。
        /// </summary>
        public List<PageBlocks> Paragraphs { get; set; } = new List<PageBlocks>();

        public ContentNode(string title, int level)
        {
            Title = title;
            Level = level;
        }

        public override string ToString()
        {
            // 主要用于调试时方便查看
            return $"{new string(' ', Level * 2)}- {Title} (Page: {StartPage}, Paragraphs: {Paragraphs.Count})";
        }
    }

    public class PageBlocks
    {
        public PageBlocks(IReadOnlyList<TextBlock> blocks, int pageNumber)
        {
            Blocks = blocks;
            PageNumber = pageNumber;
        }

        public int PageNumber { get; }

        public IReadOnlyList<TextBlock> Blocks { get; }
    }

    private readonly ConcurrentDictionary<int, PageCacheItem> _cache;

    public PdfDocument Document { get; }

    public PDFExtractor(string filePath)
    {
        Document = PdfDocument.Open(filePath);
        _cache = new ConcurrentDictionary<int, PageCacheItem>();
    }

    public void Initialize(Thickness? padding = null)
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
            _cache.TryAdd(page.Number, new PageCacheItem(page, page.Number, blocks));
        }

        var orderDetector = new UnsupervisedReadingOrderDetector(10);
        foreach (var pageCacheItem in _cache.Values)
        {
            var orderedBlocks = orderDetector.Get(pageCacheItem.TotalTextBlocks).ToArray();
            pageCacheItem.RemainingTextBlocks = orderedBlocks.ToList();
            pageCacheItem.TotalTextBlocks = orderedBlocks;
        }
    }

    /// <summary>
    /// 解析出内容树。
    /// </summary>
    /// <returns>内容树的根节点列表。</returns>
    private List<ContentNode> ExtractContentTree()
    {
        if (!Document.TryGetBookmarks(out var bookmarks))
        {
            // 如果没有书签，可以返回一个代表整个文档的单一节点
            // 或者直接返回空列表，表示无法按目录结构解析
            return new List<ContentNode>();
        }

        var rootNodes = new List<ContentNode>();
        foreach (var bookmark in bookmarks.Roots)
        {
            rootNodes.Add(BuildNodeTree(bookmark));
        }

        return rootNodes;
    }

    public IList<ContentNode> Analyze()
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

    private Point GetTopLeft(ContentNode node)
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
    private ContentNode BuildNodeTree(BookmarkNode bookmark)
    {
        // 尝试获取书签指向的页码
        // DestinationProvider可以更稳定地获取页码
        var node = new ContentNode(bookmark.Title, bookmark.Level);
        if (bookmark is DocumentBookmarkNode docNode)
        {
            node.Destination = docNode.Destination;
            node.StartPage = docNode.PageNumber;
            node.StartPoint = GetTopLeft(node);
            foreach (var child in bookmark.Children)
            {
                node.Children.Add(BuildNodeTree(child));
            }
        }

        return node;
    }

    /// <summary>
    /// 将树形结构扁平化为一个列表，方便后续处理。
    /// </summary>
    private static IEnumerable<ContentNode> Flatten(IEnumerable<ContentNode> nodes)
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
    private void ExtractParagraphs(ContentNode preNode,
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
                preNode.Paragraphs.Add(new PageBlocks(page.RemainingTextBlocks.ToArray(), i));
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

                preNode.Paragraphs.Add(new PageBlocks(blocks, i));
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
    public static async Task<List<SKDocChunk>> ToSKDocChunks(this IEnumerable<PDFExtractor.ContentNode> nodes,
        string docId, Func<string, Task<string>> llmCall, CancellationToken token)
    {
        var docChunks = new List<SKDocChunk>();
        foreach (var contentNode in nodes)
        {
            await ApplyRaptor(docId, contentNode, docChunks, llmCall);
        }

        return docChunks;
    }
    
    private static async Task<SKDocChunk> ApplyRaptor(string docId, PDFExtractor.ContentNode node,
        List<SKDocChunk> chunks,
        Func<string, Task<string>> llmCall, Guid? parentId = null)
    {
        var nodeLevel = node.Level;
        var nodeChunk = new SKDocChunk()
        {
            Key = Guid.NewGuid(),
            DocumentId = docId,
            ParentKey = parentId ?? Guid.Empty,
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
                var chunk = await ApplyRaptor(docId, child, chunks, llmCall, nodeChunk.Key);
                summaryBuilder.AppendLine($"- {child.Title}");
                summaryBuilder.AppendLine(chunk.Summary);
            }

            var chunkText = await llmCall(summaryBuilder.ToString());
            nodeChunk.Summary = chunkText;
        }
        else
        {
            var nodeContentBuilder = new StringBuilder();
            if (node.Paragraphs.Count > 0)
            {
                foreach (var paragraph in node.Paragraphs)
                {
                    var paragraphContentBuilder = new StringBuilder();
                    if (!paragraph.Blocks.Any())
                    {
                        continue;
                    }

                    foreach (var block in paragraph.Blocks)
                    {
                        paragraphContentBuilder.AppendLine(block.Text);
                    }

                    var paragraphContent = paragraphContentBuilder.ToString();
                    if (string.IsNullOrEmpty(paragraphContent.Trim()))
                    {
                        continue; // 跳过空段落
                    }

                    nodeContentBuilder.AppendLine(paragraphContent);
                    chunks.Add(new SKDocChunk()
                    {
                        Key = Guid.NewGuid(),
                        DocumentId = docId,
                        Text = paragraphContent,
                        Summary = await llmCall(paragraphContent),
                        Level = nodeLevel + 1,
                        ParentKey = nodeChunk.Key,
                        HasChild = false
                    });
                }

                nodeChunk.HasChild = true;
            }

            var nodeContent = nodeContentBuilder.ToString();
            if (string.IsNullOrEmpty(nodeContent.Trim()))
            {
                nodeChunk.HasChild = false;
                nodeChunk.Text = node.Title;
                nodeChunk.Summary = node.Title;
            }
            else
            {
                //存在段落
                nodeChunk.HasChild = false;
                nodeChunk.Summary = await llmCall(nodeContent);
                //存在子节点时，Text为空，表示需要进一步查找
            }
        }

        chunks.Add(nodeChunk);
        return nodeChunk;
    }
}