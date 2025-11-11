using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LLMClient.Data;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Graphics.Colors;
using UglyToad.PdfPig.Images;
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

    private static string GetExtensionFromFilter(NameToken filterName)
    {
        // DCT解码 - JPEG格式  
        if (filterName.Equals(NameToken.DctDecode) || filterName.Equals(NameToken.DctDecodeAbbreviation))
        {
            return ".jpg";
        }

        // JPX解码 - JPEG2000格式  
        if (filterName.Equals(NameToken.JpxDecode))
        {
            return ".jp2";
        }

        // JBIG2解码 - JBIG2格式  
        if (filterName.Equals(NameToken.Jbig2Decode))
        {
            return ".jb2";
        }

        // CCITT传真解码 - TIFF格式  
        if (filterName.Equals(NameToken.CcittfaxDecode) || filterName.Equals(NameToken.CcittfaxDecodeAbbreviation))
        {
            return ".tiff";
        }

        // Flate/LZW/RunLength/ASCII解码 - 通常是原始位图数据  
        if (filterName.Equals(NameToken.FlateDecode) || filterName.Equals(NameToken.FlateDecodeAbbreviation) ||
            filterName.Equals(NameToken.LzwDecode) || filterName.Equals(NameToken.LzwDecodeAbbreviation) ||
            filterName.Equals(NameToken.RunLengthDecode) || filterName.Equals(NameToken.RunLengthDecodeAbbreviation) ||
            filterName.Equals(NameToken.Ascii85Decode) || filterName.Equals(NameToken.Ascii85DecodeAbbreviation) ||
            filterName.Equals(NameToken.AsciiHexDecode) || filterName.Equals(NameToken.AsciiHexDecodeAbbreviation))
        {
            return ".bmp"; // 原始位图数据  
        }

        return ".jpg"; // 默认  
    }

    public static bool TryGetBytes(this IPdfImage pdfImage, [NotNullWhen(true)] out byte[]? imageBytes,
        [NotNullWhen(true)] out string? extension, out bool shouldInvertColors)
    {
        extension = null;
        imageBytes = null;
        if (pdfImage.TryGetPng(out imageBytes))
        {
            extension = ".png";
            shouldInvertColors = ShouldInvertColors(pdfImage);
            return true;
        }

        if (pdfImage.TryGetBytesAsMemory(out var memory))
        {
            imageBytes = memory.ToArray();
            extension = ".jpg";
            shouldInvertColors = ShouldInvertColors(pdfImage);
            return true;
        }

        shouldInvertColors = false;
        if (pdfImage.ImageDictionary.TryGet(NameToken.Filter, out var token))
        {
            shouldInvertColors = true;
            if (token is NameToken name)
            {
                imageBytes = pdfImage.RawMemory.ToArray();
                extension = GetExtensionFromFilter(name);
            }
            else if (token is ArrayToken arrayToken)
            {
                // 处理 Filter 数组：取最后一个（PDF Filter 从右到左应用，主要压缩通常在最后）
                var names = arrayToken.Data.OfType<NameToken>().ToArray();
                if (names.Length != 0)
                {
                    imageBytes = pdfImage.RawMemory.ToArray();
                    extension = GetExtensionFromFilter(names.Last());
                }
            }
        }

        return extension != null;
    }

    /// <summary>
    /// Checks the image's Decode array to determine if its colors should be inverted.
    /// This is the most reliable method based on the PDF specification.
    /// </summary>
    /// <param name="image">The IPdfImage to check.</param>
    /// <returns>True if the image colors are inverted, otherwise false.</returns>
    public static bool ShouldInvertColors(this IPdfImage image)
    {
        // 1. Get the Decode array from the image dictionary.
        if (!image.ImageDictionary.TryGet(NameToken.Decode, out var decodeToken) ||
            !(decodeToken is ArrayToken decodeArray))
        {
            // If /Decode array is missing, it uses the default mapping, which is not inverted.
            return false;
        }

        // 2. Analyze the Decode array based on the color space.
        var colorSpace = image.ColorSpaceDetails;

        if (colorSpace is DeviceGrayColorSpaceDetails)
        {
            // For DeviceGray, the default is [0 1]. Inverted is [1 0].
            if (decodeArray.Data.Count == 2 &&
                decodeArray.Data[0] is NumericToken first &&
                decodeArray.Data[1] is NumericToken second)
            {
                return first.Int == 1 && second.Int == 0;
            }
        }
        else if (colorSpace is DeviceRgbColorSpaceDetails)
        {
            // For DeviceRGB, default is [0 1 0 1 0 1]. Inverted is [1 0 1 0 1 0].
            if (decodeArray.Data.Count == 6 &&
                IsNumericSequence(decodeArray.Data, new[] { 1, 0, 1, 0, 1, 0 }))
            {
                return true;
            }
        }
        else if (colorSpace is IndexedColorSpaceDetails indexed)
        {
            // For Indexed color spaces, inversion is complex. The Decode array applies
            // to the palette indices, not the final colors. A [1 0] might mean it's
            // using the color palette in reverse. True inversion detection would require
            // analyzing the palette itself. However, a simple check for [1 0] is a
            // reasonable heuristic if the base color space is Gray.
            if (indexed.BaseColorSpace is DeviceGrayColorSpaceDetails &&
                decodeArray.Data.Count == 2 &&
                IsNumericSequence(decodeArray.Data, new[] { 1, 0 }))
            {
                return true;
            }
        }
        // Note: CMYK inversion ([1 0 1 0 1 0 1 0]) is also possible but less common.
        // Can be added if needed.

        return false;
    }

    private static bool IsNumericSequence(IReadOnlyList<IToken> tokens, int[] expected)
    {
        if (tokens.Count != expected.Length) return false;
        for (int i = 0; i < tokens.Count; i++)
        {
            if (!(tokens[i] is NumericToken num) || num.Int != expected[i])
            {
                return false;
            }
        }

        return true;
    }

    public static ImageSource? ToImageSource(this IPdfImage pdfImage)
    {
        var imageBounds = pdfImage.Bounds;
        try
        {
            if (pdfImage.TryGetBytes(out var imageBytes, out var extension, out var shouldInvertColors))
            {
                using (var memoryStream = new MemoryStream(imageBytes))
                {
                    return memoryStream.ToImageSource(extension,
                        new Size((int)imageBounds.Width, (int)imageBounds.Height),
                        shouldInvertColors);
                }
            }
        }
        catch (Exception)
        {
            // ignored
        }

        //尝试手动解码
        try
        {
            var bitmapSourceFromDecoded = CreateBitmapSourceFromDecoded(pdfImage, pdfImage.RawMemory,
                pdfImage.WidthInSamples, pdfImage.HeightInSamples);
            bitmapSourceFromDecoded?.Freeze();
            return bitmapSourceFromDecoded;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 从解码字节创建 BitmapSource（使用 ColorSpaceDetailsByteConverter 转换为 RGB，然后适配 WPF 的 BGR24）。
    /// </summary>
    private static BitmapSource? CreateBitmapSourceFromDecoded(IPdfImage pdfImage, ReadOnlyMemory<byte> decoded,
        int width, int height)
    {
        var spaceDetails = pdfImage.ColorSpaceDetails;
        if (spaceDetails == null) return null;
        ReadOnlySpan<byte> rgbBytes = ColorSpaceDetailsByteConverter.Convert(
            spaceDetails,
            decoded.ToArray(),
            pdfImage.BitsPerComponent,
            width,
            height
        );

        // 根据颜色组件数动态计算长度
        int channelCount = spaceDetails.NumberOfColorComponents;
        int requiredLength = width * height * channelCount;

        if (rgbBytes.Length < requiredLength)
            return null;

        // ===== 处理不同颜色空间 ===== //
        switch (channelCount)
        {
            // 处理灰度图像 (Lab/DeviceGray 等)
            case 1:
                return BitmapSource.Create(
                    width, height, 96, 96,
                    PixelFormats.Gray8, // 标准 8位灰度
                    null,
                    rgbBytes[..requiredLength].ToArray(),
                    width // Stride = width (1字节/像素)
                );

            // 处理 RGB 图像
            case 3:
                byte[] bgrBytes = new byte[requiredLength];
                // 交换 R 和 B (R G B -> B G R)
                for (int i = 0; i < rgbBytes.Length; i += 3)
                {
                    bgrBytes[i] = rgbBytes[i + 2];
                    bgrBytes[i + 1] = rgbBytes[i + 1];
                    bgrBytes[i + 2] = rgbBytes[i];
                }

                return BitmapSource.Create(
                    width, height, 96, 96,
                    PixelFormats.Bgr24,
                    null,
                    bgrBytes,
                    width * 3
                );

            // 处理 CMYK 图像 (PdfPig 已转换为 RGB)
            case 4:
                // 注意: Convert() 已转为 RGB，所以 channelCount 实际为 3
                // 此处仅为完整性
                break;
        }

        return null;
    }
}