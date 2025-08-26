using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LLMClient.Data;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.Graphics.Colors;
using UglyToad.PdfPig.Tokens;

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

[Experimental("ManualPdfImageDecoder")]
public static class ManualPdfImageDecoder
{
    public static BitmapSource? DecodeToImageSource(IPdfImage pdfImage)
    {
        var rawMemory = pdfImage.RawMemory;
        if (rawMemory.IsEmpty) return null;

        byte[] decodedBytes = rawMemory.ToArray(); // 默认 raw

        if (!pdfImage.ImageDictionary.TryGet(NameToken.Filter, out var token))
        {
            return null;
        }

        if (pdfImage.ColorSpaceDetails == null)
        {
            return null;
        }

        decodedBytes = ApplyFilter((NameToken)token, decodedBytes); // 自定义应用
        // 根据 ColorSpaceDetails 转换（简化版，参考 PdfPig 的 ColorSpaceDetailsByteConverter）
        byte[] pixelBytes = ConvertToPixels(
            decodedBytes,
            pdfImage.ColorSpaceDetails,
            pdfImage.BitsPerComponent,
            pdfImage.WidthInSamples,
            pdfImage.HeightInSamples
        );

        // 创建 WPF BitmapSource
        PixelFormat format = GetPixelFormat(pdfImage.ColorSpaceDetails, pdfImage.BitsPerComponent);
        var bitmap = new WriteableBitmap(pdfImage.WidthInSamples, pdfImage.HeightInSamples, 96, 96, format, null);
        bitmap.WritePixels(new Int32Rect(0, 0, pdfImage.WidthInSamples, pdfImage.HeightInSamples), pixelBytes,
            bitmap.BackBufferStride, 0);
        return bitmap;
    }

    private static byte[] ApplyFilter(NameToken filter, byte[] input)
    {
        // 示例：处理常见过滤器
        switch (filter.Data)
        {
            case "DCTDecode": // JPEG，直接返回（.NET 可加载）
                return input;
            case "FlateDecode":
            {
                using var inputStream = new MemoryStream(input);
                using var deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress);
                using var outputStream = new MemoryStream();
                deflateStream.CopyTo(outputStream);
                return outputStream.ToArray();
            }
            // 添加更多：如 JBIG2 需要第三方库
            default:
                throw new NotSupportedException($"Unsupported filter: {filter.Data}");
        }
    }

    private static byte[] ConvertToPixels(byte[] decoded, ColorSpaceDetails colorSpace, int bitsPerComponent,
        int width, int height)
    {
        // 简化转换：假设 RGB (实际需根据 colorSpace.Type 调整)
        // 参考 PdfPig: 解包 bits, 应用 Decode, 转换颜色
        if (colorSpace is DeviceRgbColorSpaceDetails)
        {
            // RGB: 每个像素 3 组件
            int bytesPerPixel = 3 * (bitsPerComponent / 8);
            // 这里省略详细转换逻辑（可从 PdfPig 源复制）；返回 RGBA 字节以兼容 WPF
            return decoded; // 占位，实际实现转换
        }

        // 其他空间类似
        throw new NotSupportedException("Unsupported color space");
    }

    private static PixelFormat GetPixelFormat(ColorSpaceDetails colorSpace, int bitsPerComponent)
    {
        // 示例映射
        if (colorSpace is DeviceRgbColorSpaceDetails) return PixelFormats.Bgr24;
        if (colorSpace is DeviceGrayColorSpaceDetails)
            return bitsPerComponent == 8 ? PixelFormats.Gray8 : PixelFormats.Gray16;
        return PixelFormats.Default;
    }
}