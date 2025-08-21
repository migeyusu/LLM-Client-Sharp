using System.Text.RegularExpressions;

namespace LLMClient.Rag.Document;

public static class HeadingParser
{
    // 匹配：前导空白 + n级编号(1 或 1.1 或 1.2.3 或 1-2-3) + 可选的'.'或')' + 空白 + 标题
    private static readonly Regex HeadingNumberRegex = new Regex(
        @"^\s*(\d+(?:[.\-]\d+)*)[.)]?\s+(?<title>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // 尝试解析，成功返回 true，并输出编号、标题、各级数字
    public static bool TryParse(string text, out string numbering, out string title, out int[] levels)
    {
        numbering = string.Empty;
        title = string.Empty;
        levels = Array.Empty<int>();

        if (string.IsNullOrWhiteSpace(text)) return false;

        var m = HeadingNumberRegex.Match(text);
        if (!m.Success) return false;

        numbering = m.Groups[1].Value; // 如 "1.1.1"
        title = m.Groups["title"].Value.Trim(); // 如 "xxxx"

        // 将编号拆分为各级整数，忽略非整数段
        levels = numbering
            .Split(new[] { '.', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var n) ? n : (int?)null)
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .ToArray();

        return true;
    }

    // 仅提取编号；不匹配则返回 null
    public static string? ExtractNumbering(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = HeadingNumberRegex.Match(text);
        return m.Success ? m.Groups[1].Value : null;
    }
}