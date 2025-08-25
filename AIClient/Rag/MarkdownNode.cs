using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LLMClient.Data;
using LLMClient.Rag.Document;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Logging;
using MimeTypes;

namespace LLMClient.Rag;

/// <summary>
/// 表示Markdown文档中的一个内容节点，通常对应一个标题（Heading）。
/// </summary>
public class MarkdownNode : RawNode<MarkdownNode, MarkdownContent>
{
    public MarkdownNode(string title, int level) : base(title)
    {
        Level = level;
    }

    public override string ToString()
    {
        // 用于调试时方便查看
        var indent = new string(' ', Level * 2);
        return $"{indent}- {Title} (Level: {Level})";
    }
}

public class MarkdownContent : IContentUnit
{
    private readonly string _markdownFilePath;

    /// <summary>
    /// 该节点直接包含的Markdown内容。
    /// 通常是标题下的所有段落、列表、代码块等的原始内容拼接。
    /// </summary>
    public string Content { get; }

    private readonly string[] _imageLinks;

    public MarkdownContent(Block block, string rawMarkdownText, string markdownFilePath)
    {
        _markdownFilePath = markdownFilePath ?? throw new ArgumentNullException(nameof(markdownFilePath));
        // 我们获取该块的原始Markdown文本
        this.Content = rawMarkdownText.Substring(block.Span.Start, block.Span.Length);
        if (block is LeafBlock leafBlock)
        {
            _imageLinks = leafBlock.Inline.Descendants<LinkInline>()
                .Where(link => link.IsImage && !string.IsNullOrEmpty(link.Url))
                .Select(link => link.Url!) // link.Url 在此上下文中不会为null
                .ToArray();
        }
        else
        {
            _imageLinks = [];
        }
    }

    private List<string>? _imageList;

    public async Task<IList<string>> GetImages(ILogger? logger)
    {
        if (_imageList == null)
        {
            _imageList = [];
            foreach (var imageLink in _imageLinks)
            {
                if (imageLink.StartsWith(ImageExtensions.Base64ImagePrefix))
                {
                    // 已经是base64编码的图像
                    _imageList.Add(imageLink);
                    continue;
                }

                if (Uri.TryCreate(imageLink, UriKind.RelativeOrAbsolute, out var uri))
                {
                    Debug.Write(uri);
                    if (uri.IsAbsoluteUri)
                    {
                        string? filePath = null;
                        if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                        {
                            // 远程URL，下载
                            filePath = await HttpContentCache.Instance.GetOrCreateAsync(uri.AbsoluteUri);
                        }
                        else if (uri.IsFile)
                        {
                            filePath = uri.LocalPath;
                        }

                        if (filePath != null)
                        {
                            try
                            {
                                var fileInfo = new FileInfo(uri.LocalPath);
                                if (fileInfo.Exists)
                                {
                                    await using (var fileStream = fileInfo.OpenRead())
                                    {
                                        _imageList.Add(
                                            await fileStream.ToBase64StringAsync(Path.GetExtension(fileInfo.FullName)));
                                        continue;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                logger?.LogWarning(ex, "无法下载图像：{ImageUrl}", imageLink);
                            }
                        }
                    }
                }

                if (!Path.IsPathRooted(imageLink))
                {
                    var markdownDir = Path.GetDirectoryName(_markdownFilePath);
                    if (markdownDir != null)
                    {
                        var imagePath = Path.Combine(markdownDir, imageLink);
                        var fileInfo = new FileInfo(imagePath);
                        if (fileInfo.Exists)
                        {
                            await using var fileStream = fileInfo.OpenRead();
                            _imageList.Add(await fileStream.ToBase64StringAsync(fileInfo.Extension));
                        }
                    }
                }
            }
        }

        return _imageList;
    }
}