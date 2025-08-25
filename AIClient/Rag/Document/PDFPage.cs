using System.IO;
using System.Text;
using LLMClient.Data;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis;

namespace LLMClient.Rag.Document;

public class PDFPage : IContentUnit
{
    public PDFPage(IReadOnlyList<TextBlock> blocks, int pageNumber, IReadOnlyList<IPdfImage> images)
    {
        Blocks = blocks;
        PageNumber = pageNumber;
        PdfImages = images;
        var paragraphContentBuilder = new StringBuilder();
        foreach (var block in this.Blocks)
        {
            paragraphContentBuilder.AppendLine(block.Text);
        }

        Content = paragraphContentBuilder.ToString();
    }

    public int PageNumber { get; }

    public IReadOnlyList<TextBlock> Blocks { get; }

    public string Content { get; }

    private IList<string>? _images;

    public async Task<IList<string>> GetImages(ILogger? logger)
    {
        if (_images != null) return _images;
        var imageTasks = this.PdfImages.Select(async image =>
        {
            // 首先尝试转换为PNG字节（推荐，用于大多数图像）
            if (image.TryGetPng(out var imageBytes))
            {
                using (var memoryStream = new MemoryStream(imageBytes))
                {
                    return await memoryStream.ToBase64StringAsync(".png");
                }
            }

            if (image.TryGetBytesAsMemory(out var rawBytes))
            {
                using (var memoryStream = new MemoryStream(rawBytes.ToArray()))
                {
                    return await memoryStream.ToBase64StringAsync(".jpg");
                }
            }

            logger?.LogWarning("无法处理图像，所在页码：{PageNumber}", PageNumber);
            return string.Empty; // 如果无法处理图像，返回空字符串
        }).ToArray();

        var results = await Task.WhenAll(imageTasks);
        _images = results.Where(s => !string.IsNullOrEmpty(s)).ToList();

        return _images.ToArray();
    }

    public IReadOnlyList<IPdfImage> PdfImages { get; }
}