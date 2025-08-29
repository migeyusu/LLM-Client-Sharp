using System.IO;
using System.Text;
using System.Windows.Media;
using LLMClient.Data;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Tokens;

namespace LLMClient.Rag.Document;

public static class ExtractorExtensions
{
    /// <summary>
    /// 节点进行摘要处理，使用LLM生成每个节点的摘要。
    /// </summary>
    /// <param name="node"></param>
    /// <param name="llmCall"></param>
    /// <param name="logger"></param>
    /// <param name="nodeProgress"></param>
    /// <param name="token"></param>
    public static async Task GenerateSummarize<T, TK>(this T node,
        Func<T, CancellationToken, Task<string>> llmCall, ILogger? logger = null,
        IProgress<T>? nodeProgress = null, CancellationToken token = default)
        where T : RawNode<T, TK>
        where TK : IContentUnit
    {
        token.ThrowIfCancellationRequested();
        if (node.HasChildren)
        {
            await Parallel.ForEachAsync(node.Children, new ParallelOptions() { CancellationToken = token },
                (async (pdfNode, cancellationToken) =>
                {
                    using (var cancellationTokenSource =
                           CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        try
                        {
                            await pdfNode.GenerateSummarize<T, TK>(llmCall, logger, nodeProgress,
                                cancellationTokenSource.Token);
                        }
                        catch (Exception)
                        {
                            await cancellationTokenSource.CancelAsync();
                            throw;
                        }
                    }
                }));
            // 生成摘要：子节点标题 + 摘要
            node.Summary = await llmCall(node, token);
        }
        else
        {
            node.Summary = await llmCall(node, token);
        }

        nodeProgress?.Report(node);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="nodes">
    ///     顶层节点
    ///     <para><b>警告：</b> 请勿传入已扁平化的节点列表，否则结果不正确。</para>
    /// </param>
    /// <param name="docId"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static async Task<List<DocChunk>> ToDocChunks<T, TK>(this IList<T> nodes,
        string docId, ILogger? logger = null)
        where T : RawNode<T, TK>
        where TK : IContentUnit
    {
        var docChunks = new List<DocChunk>();
        for (var index = 0; index < nodes.Count; index++)
        {
            var contentNode = nodes[index];
            await ApplyRaptor<T, TK>(docId, contentNode, index, docChunks, logger);
        }

        return docChunks;
    }

    private static async Task ApplyRaptor<T, TK>(string docId, T node, int nodeIndex,
        List<DocChunk> chunks, ILogger? logger = null, string? parentId = null)
        where T : RawNode<T, TK>
        where TK : IContentUnit
    {
        var nodeLevel = node.Level;
        var nodeChunk = new DocChunk()
        {
            Key = Guid.NewGuid().ToString(),
            DocumentId = docId,
            ParentKey = parentId ?? string.Empty,
            Level = nodeLevel,
            Title = node.Title,
            Index = nodeIndex,
            Type = (int)ChunkType.Node, // 表示书签类型
        };
        if (node.HasChildren)
        {
            nodeChunk.HasChildNode = true;
            var children = node.Children;
            foreach (var child in children)
            {
                await ApplyRaptor<T, TK>(docId, child, chunks.Count, chunks, logger,
                    nodeChunk.Key);
            }
        }
        else
        {
            var units = node.ContentUnits;
            if (units.Count > 0)
            {
                for (var index = 0; index < units.Count; index++)
                {
                    var page = units[index];
                    try
                    {
                        var pageContent = page.Content;
                        var images = await page.GetImages(logger);
                        if (string.IsNullOrEmpty(pageContent.Trim()) && images.Count == 0)
                        {
                            logger?.LogWarning("跳过空内容，所在节点：{0}，节点索引：{1}", node.Title, index);
                            continue; // 跳过空段落
                        }

                        var pageChunk = new DocChunk()
                        {
                            Key = Guid.NewGuid().ToString(),
                            DocumentId = docId,
                            Text = pageContent,
                            Level = nodeLevel + 1,
                            Index = index,
                            ParentKey = nodeChunk.Key,
                            Type = (int)ChunkType.ContentUnit, // 表示段落类型
                        };
                        pageChunk.SetImages(images);
                        chunks.Add(pageChunk);
                    }
                    catch (Exception e)
                    {
                        logger?.LogError(e, "处理段落时出错，所在节点：{0}，节点索引：{1}", node.Title, index);
                    }
                }
            }
        }

        //存在子节点时，Text为空，表示需要进一步查找
        var nodeSummary = node.Summary;
        /*if (string.IsNullOrEmpty(nodeSummary.Trim()))
        {
            logger?.LogWarning("节点没有内容，不会添加。所在页码：{StartPage}, 标题：{Title}", node.StartPage, node.Title);
            return;
        }*/
        nodeChunk.Summary = nodeSummary;
        chunks.Add(nodeChunk);
    }

    /// <summary>
    /// 将树形结构扁平化为一个列表，方便后续处理。
    /// </summary>
    public static IEnumerable<PDFNode> Flatten(this IEnumerable<PDFNode> nodes)
    {
        return nodes.SelectMany(n => new[] { n }.Concat(Flatten(n.Children)));
    }

    public static ImageSource? ToImageSource(this IPdfImage pdfImage)
    {
        var imageBounds = pdfImage.Bounds;
        byte[]? imageBytes = null;
        string extension = string.Empty;
        try
        {
            if (pdfImage.TryGetPng(out imageBytes))
            {
                extension = ".png";
            }

            if (pdfImage.TryGetBytesAsMemory(out var memory))
            {
                imageBytes = memory.ToArray();
                extension = ".jpg";
            }

            if (pdfImage.ImageDictionary.TryGet(NameToken.Filter, out var token) && token.Equals(NameToken.DctDecode))
            {
                imageBytes = pdfImage.RawMemory.ToArray();
                extension = ".jpg";
            }

            if (imageBytes != null)
            {
                using (var memoryStream = new MemoryStream(imageBytes))
                {
                    return memoryStream.ToImageSource(extension, (uint)imageBounds.Width, (uint)imageBounds.Height);
                }
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}