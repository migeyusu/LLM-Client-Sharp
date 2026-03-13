// File: LLMClient/ContextEngineering/Tools/Models/TextSearchResult.cs

namespace LLMClient.ContextEngineering.Tools.Models;

/// <summary>
/// 单条文本搜索匹配结果
/// </summary>
public sealed record TextSearchResult
{
    public string FilePath { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public int LineNumber { get; init; }
    public int Column { get; init; }
    public string LineContent { get; init; } = string.Empty;
    public string? ContextBefore { get; init; }
    public string? ContextAfter { get; init; }
}

/// <summary>
/// 文本搜索的整体视图
/// </summary>
public sealed record TextSearchView
{
    public string Query { get; init; } = string.Empty;
    public string SearchMode { get; init; } = string.Empty; // "Text" | "Regex"
    public int TotalMatches { get; init; }
    public int FilesSearched { get; init; }
    public bool Truncated { get; init; }
    public List<TextSearchResult> Results { get; init; } = [];
}