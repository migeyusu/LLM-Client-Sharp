using System.IO;
using System.Text;
using LLMClient.Data;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig.Content;

namespace LLMClient.Rag.Document;

public static class PDFExtractorExtensions
{
    /// <summary>
    /// 对PDF节点进行摘要处理，使用LLM生成每个节点的摘要。
    /// </summary>
    /// <param name="node"></param>
    /// <param name="llmCall"></param>
    /// <param name="logger"></param>
    /// <param name="nodeProgress"></param>
    /// <param name="token"></param>
    public static async Task GenerateSummarize(this PDFNode node,
        Func<string, CancellationToken, Task<string>> llmCall, ILogger? logger = null,
        IProgress<PDFNode>? nodeProgress = null, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (node.HasChildren)
        {
            await Parallel.ForEachAsync(node.Children, new ParallelOptions(),
                (async (pdfNode, cancellationToken) =>
                {
                    await pdfNode.GenerateSummarize(llmCall, logger, nodeProgress, cancellationToken);
                }));
            // 生成摘要：子节点标题 + 摘要
            var summaryBuilder = new StringBuilder(node.Title + "\nSubsections:");
            foreach (var childNode in node.Children)
            {
                summaryBuilder.AppendLine($"- {childNode.Title}");
                summaryBuilder.AppendLine(childNode.Summary);
            }

            node.Summary = await llmCall(summaryBuilder.ToString(), token);
            //text 为空
        }
        else
        {
            var nodeContentBuilder = new StringBuilder();
            var pages = node.Pages;
            if (pages.Count > 0)
            {
                for (var index = 0; index < pages.Count; index++)
                {
                    var page = pages[index];
                    try
                    {
                        //note: paragraph不执行summary
                        var pageContent = page.Content;
                        if (string.IsNullOrEmpty(pageContent.Trim()))
                        {
                            logger?.LogWarning("跳过空页，所在页码：{PageNumber}", page.PageNumber);
                            continue; // 跳过空段落
                        }

                        nodeContentBuilder.AppendLine(pageContent);
                    }
                    catch (Exception e)
                    {
                        logger?.LogError(e, "处理段落时出错，所在页码：{PageNumber}", page.PageNumber);
                    }
                }
            }

            var nodeContent = nodeContentBuilder.ToString();
            if (!string.IsNullOrEmpty(nodeContent.Trim()))
            {
                node.Summary = await llmCall(nodeContent, token);
            }
            else
            {
                logger?.LogWarning("节点没有内容，不会添加。所在页码：{StartPage}, 标题：{Title}", node.StartPage, node.Title);
            }
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
    public static List<DocChunk> ToDocChunks(this IList<PDFNode> nodes,
        string docId, ILogger? logger = null)
    {
        var docChunks = new List<DocChunk>();
        for (var index = 0; index < nodes.Count; index++)
        {
            var contentNode = nodes[index];
            ApplyRaptor(docId, contentNode, index, docChunks, logger);
        }

        return docChunks;
    }

    private static void ApplyRaptor(string docId, PDFNode node, int nodeIndex,
        List<DocChunk> chunks, ILogger? logger = null, string? parentId = null)
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
            Type = (int)ChunkType.Bookmark, // 表示书签类型
        };
        if (node.HasChildren)
        {
            nodeChunk.HasChildNode = true;
            var children = node.Children;
            foreach (var child in children)
            {
                ApplyRaptor(docId, child, chunks.Count, chunks, logger,
                    nodeChunk.Key);
            }
        }
        else
        {
            var pages = node.Pages;
            if (pages.Count > 0)
            {
                for (var index = 0; index < pages.Count; index++)
                {
                    var page = pages[index];
                    try
                    {
                        var pageContent = page.Content;
                        var images = page.Images;
                        if (string.IsNullOrEmpty(pageContent.Trim()) && images.Count == 0)
                        {
                            logger?.LogWarning("跳过空页，所在页码：{PageNumber}", page.PageNumber);
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
                            Type = (int)ChunkType.Page, // 表示段落类型
                        };
                        var base64Images = images.Select(image =>
                        {
                            // 首先尝试转换为PNG字节（推荐，用于大多数图像）
                            if (image.TryGetPng(out var imageBytes))
                            {
                                using (var memoryStream = new MemoryStream(imageBytes))
                                {
                                    return memoryStream.ToBase64String(".png");
                                }
                            }

                            if (image.TryGetBytesAsMemory(out var rawBytes))
                            {
                                using (var memoryStream = new MemoryStream(rawBytes.ToArray()))
                                {
                                    return memoryStream.ToBase64String(".jpg");
                                }
                            }

                            logger?.LogWarning("无法处理图像，所在页码：{PageNumber}", page.PageNumber);
                            return string.Empty; // 如果无法处理图像，返回空字符串
                        }).ToArray();
                        pageChunk.SetImages(base64Images);
                        chunks.Add(pageChunk);
                    }
                    catch (Exception e)
                    {
                        logger?.LogError(e, "处理段落时出错，所在页码：{PageNumber}", page.PageNumber);
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
}