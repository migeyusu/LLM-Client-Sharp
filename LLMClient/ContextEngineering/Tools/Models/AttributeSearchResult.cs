// File: LLMClient/ContextEngineering/Tools/Models/AttributeSearchResult.cs

namespace LLMClient.ContextEngineering.Tools.Models;

/// <summary>
/// 特性标注搜索结果
/// </summary>
public sealed record AttributeSearchResult : DescribedSymbolViewBase
{
    public List<string> Attributes { get; init; } = [];
    public LocationView? Location { get; init; }
}

/// <summary>
/// 特性搜索整体视图
/// </summary>
public sealed record AttributeSearchView
{
    public string AttributeName { get; init; } = string.Empty;
    public int TotalCount { get; init; }
    public List<AttributeSearchResult> Results { get; init; } = [];
}