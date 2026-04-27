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
        var content = DecodeBytes(bytes, encoding);
        return (content, encoding);
    }

    public static string DecodeBytes(byte[] bytes, Encoding encoding)
    {
        var content = encoding.GetString(bytes);

        // Strip the BOM character (U+FEFF) if present in the decoded string.
        // encoding.GetString() converts BOM bytes into U+FEFF in the text,
        // which causes a spurious ZWNBSP character when the content is later
        // written back with a BOM-emitting encoding (e.g., UTF8Encoding(true)).
        // The encoding object itself already carries the "emit BOM" flag,
        // so File.WriteAllTextAsync will re-emit the BOM bytes automatically.
        if (content.Length > 0 && content[0] == '\uFEFF')
        {
            content = content.Substring(1);
        }

        return content;
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

        // === 1.5 UTF-16 无 BOM 启发式检测 ===
        // 对于纯 ASCII 文本的 UTF-16（无 BOM），字节通常呈现“隔位为 0”的模式：
        // LE: [非0,0,非0,0,...]，BE: [0,非0,0,非0,...]
        // 该启发式用于补充 UTF.Unknown 在无 BOM 场景下可能返回空结果的情况。
        if (TryDetectUtf16WithoutBom(bytes, out var utf16NoBomEncoding))
        {
            return utf16NoBomEncoding;
        }

        // === 2. 使用 UTF.Unknown 进行精确检测（核心）===
        try
        {
            var result = CharsetDetector.DetectFromBytes(bytes);
            var best = result.Detected;

            if (best != null && best.Encoding != null)
            {
                // best.EncodingName 可能是 "utf-8", "gb18030", "big5" 等
                // 关键：根据原始字节是否带 BOM，规范化为“可回写保持一致”的编码实例。
                return NormalizeEncodingForRoundTrip(best.Encoding, bytes);
            }
        }
        catch
        {
            // 库异常时继续执行兜底逻辑
        }

        // === 3. 最终兜底 ===
        return Encoding.Default;
    }

    private static Encoding NormalizeEncodingForRoundTrip(Encoding detectedEncoding, byte[] bytes)
    {
        var hasBom = HasPreamble(bytes, detectedEncoding.GetPreamble());

        return detectedEncoding.CodePage switch
        {
            // UTF-8
            65001 => new UTF8Encoding(hasBom),

            // UTF-16 LE / BE
            1200 => new UnicodeEncoding(bigEndian: false, byteOrderMark: hasBom),
            1201 => new UnicodeEncoding(bigEndian: true, byteOrderMark: hasBom),

            // UTF-32 LE / BE
            12000 => new UTF32Encoding(bigEndian: false, byteOrderMark: hasBom),
            12001 => new UTF32Encoding(bigEndian: true, byteOrderMark: hasBom),

            _ => detectedEncoding,
        };
    }

    private static bool HasPreamble(byte[] bytes, byte[] preamble)
    {
        if (preamble == null || preamble.Length == 0 || bytes.Length < preamble.Length)
            return false;

        for (var i = 0; i < preamble.Length; i++)
        {
            if (bytes[i] != preamble[i])
                return false;
        }

        return true;
    }

    private static bool TryDetectUtf16WithoutBom(byte[] bytes, out Encoding encoding)
    {
        encoding = null!;

        if (bytes.Length < 4 || bytes.Length % 2 != 0)
            return false;

        var evenCount = 0;
        var oddCount = 0;
        var evenNullCount = 0;
        var oddNullCount = 0;

        for (var i = 0; i < bytes.Length; i++)
        {
            if ((i & 1) == 0)
            {
                evenCount++;
                if (bytes[i] == 0)
                    evenNullCount++;
            }
            else
            {
                oddCount++;
                if (bytes[i] == 0)
                    oddNullCount++;
            }
        }

        var evenNullRatio = evenCount == 0 ? 0d : (double)evenNullCount / evenCount;
        var oddNullRatio = oddCount == 0 ? 0d : (double)oddNullCount / oddCount;

        const double highNullThreshold = 0.30;
        const double lowNullThreshold = 0.10;

        if (oddNullRatio > highNullThreshold && evenNullRatio < lowNullThreshold)
        {
            encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: false);
            return true;
        }

        if (evenNullRatio > highNullThreshold && oddNullRatio < lowNullThreshold)
        {
            encoding = new UnicodeEncoding(bigEndian: true, byteOrderMark: false);
            return true;
        }

        return false;
    }
}