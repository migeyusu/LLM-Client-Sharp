using System.Text;
using UtfUnknown;

namespace LLMClient.ToolCall.DefaultPlugins;

/// <summary>
/// Helper for detecting file encoding and reading files with proper encoding support.
/// Now powered by UTF.Unknown (based on Mozilla Universal Charset Detector) for high accuracy on CJK text.
/// </summary>
internal static class FileEncodingHelper
{
    /// <summary>
    /// Reads all text from a file with automatic encoding detection.
    /// Returns both the content string and the detected encoding (for preserving encoding on write-back).
    /// </summary>
    public static async Task<(string Content, Encoding Encoding)> ReadTextWithDetectionAsync(
        string path, CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        if (bytes.Length == 0)
            return (string.Empty, new UTF8Encoding(false));

        var encoding = DetectEncoding(bytes);
        var content = encoding.GetString(bytes);
        return (content, encoding);
    }

    /// <summary>
    /// Reads all lines from a file with automatic encoding detection.
    /// Returns both the lines and the detected encoding.
    /// </summary>
    public static async Task<(string[] Lines, Encoding Encoding)> ReadLinesWithDetectionAsync(
        string path, CancellationToken cancellationToken = default)
    {
        var (content, encoding) = await ReadTextWithDetectionAsync(path, cancellationToken);
        if (string.IsNullOrEmpty(content))
            return ([], encoding);

        var lines = content.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        return (lines, encoding);
    }

    /// <summary>
    /// Detects the encoding of a byte array using UTF.Unknown library.
    /// Strategy: BOM → UTF.Unknown high-accuracy detection → system default fallback.
    /// </summary>
    public static Encoding DetectEncoding(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return new UTF8Encoding(false);

        // === 1. BOM 快速检测（最高优先级）===
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return new UTF8Encoding(true); // UTF-8 with BOM

        if (bytes.Length >= 2)
        {
            if (bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                if (bytes.Length >= 4 && bytes[2] == 0x00 && bytes[3] == 0x00)
                    return Encoding.UTF32;
                return Encoding.Unicode; // UTF-16 LE
            }

            if (bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode; // UTF-16 BE
        }

        if (bytes.Length >= 4
            && bytes[0] == 0x00 && bytes[1] == 0x00
            && bytes[2] == 0xFE && bytes[3] == 0xFF)
            return new UTF32Encoding(true, true); // UTF-32 BE

        // === 2. 使用 UTF.Unknown 进行精确检测（核心）===
        try
        {
            var result = CharsetDetector.DetectFromBytes(bytes);
            var best = result.Detected;

            if (best != null && best.Encoding != null)
            {
                // best.EncodingName 可能是 "utf-8", "gb18030", "big5" 等
                return best.Encoding;
            }
        }
        catch
        {
            // 库异常时继续执行兜底逻辑
        }

        // === 3. 最终兜底 ===
        return Encoding.Default;
    }
}