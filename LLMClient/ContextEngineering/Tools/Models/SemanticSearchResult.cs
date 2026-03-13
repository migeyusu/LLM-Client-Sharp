// File: LLMClient/ContextEngineering/Tools/Models/SemanticSearchResult.cs

namespace LLMClient.ContextEngineering.Tools.Models;

/// <summary>
/// 语义搜索的代码片段结果
/// </summary>
public sealed record SemanticSearchResult
{
    public string FilePath { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public int StartLine { get; init; }
    public int EndLine { get; init; }
    public string CodeSnippet { get; init; } = string.Empty;
    public double SimilarityScore { get; init; }
    public string? SymbolId { get; init; }
    public string? Name { get; init; }
    public string? Summary { get; init; }
}

/// <summary>
/// 语义搜索整体视图
/// </summary>
public sealed record SemanticSearchView
{
    public string Query { get; init; } = string.Empty;
    public int TotalResults { get; init; }
    public List<SemanticSearchResult> Results { get; init; } = [];
    public string Source { get; init; } = ""; // "RAG" | "Fallback"
}