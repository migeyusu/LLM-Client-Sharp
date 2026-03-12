// File: LLMClient/ContextEngineering/Tools/Models/AttributeSearchResult.cs

namespace LLMClient.ContextEngineering.Tools.Models;

/// <summary>
/// 特性标注搜索结果
/// </summary>
public sealed class AttributeSearchResult
{
    public string SymbolId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string Signature { get; init; } = string.Empty;
    public string Accessibility { get; init; } = string.Empty;
    public List<string> Attributes { get; init; } = [];
    public string? Summary { get; init; }
    public LocationView? Location { get; init; }
}

/// <summary>
/// 特性搜索整体视图
/// </summary>
public sealed class AttributeSearchView
{
    public string AttributeName { get; init; } = string.Empty;
    public int TotalCount { get; init; }
    public List<AttributeSearchResult> Results { get; init; } = [];
}