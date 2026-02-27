namespace LLMClient.ContextEngineering.Tools.Models;

public sealed record TypeHierarchyView
{
    public string TypeId { get; init; } = "";
    public string TypeName { get; init; } = "";
    public string TypeSignature { get; init; } = "";
    /// <summary>从直接父类到 object 的有序链</summary>
    public List<string> BaseChain { get; init; } = [];
    public List<string> ImplementedInterfaces { get; init; } = [];
    /// <summary>已知派生类型（当前 solution 范围内）</summary>
    public List<SymbolBriefView> DerivedTypes { get; set; } = [];
    public string Source { get; set; } = ""; // "Roslyn" | "Index"
}