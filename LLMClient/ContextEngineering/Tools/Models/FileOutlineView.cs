// FileOutlineView.cs
namespace LLMClient.ContextEngineering.Tools.Models;

/// <summary>
/// get_file_outline 的返回结构：只含签名和位置，不含实现体。
/// 设计原则：LLM 通过 Outline 快速定位感兴趣的成员，再用 read_symbol_body 按需拉取实现。
/// </summary>
public sealed record FileOutlineView
{
    public required string FilePath { get; init; }
    public string? RelativePath { get; init; }
    public int TotalLines { get; init; }
    public List<NamespaceOutlineView> Namespaces { get; init; } = [];
}

public sealed record NamespaceOutlineView
{
    public required string Name { get; init; }
    public List<TypeOutlineView> Types { get; init; } = [];
}

public sealed record TypeOutlineView : DescribedSymbolViewBase
{
    // SymbolId, Name, Kind, Signature, Accessibility, Summary inherited
    public int StartLine { get; init; }
    public int EndLine { get; init; }
    public List<MemberOutlineView> Members { get; init; } = [];
}

public sealed record MemberOutlineView : DescribedSymbolViewBase
{
    // SymbolId, Name, Kind, Signature, Accessibility, Summary inherited
    public int StartLine { get; set; } = 0;
}