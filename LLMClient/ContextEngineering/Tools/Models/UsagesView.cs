namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class UsagesView
{
    public string SymbolId { get; init; } = "";
    public string SymbolName { get; init; } = "";
    public int TotalUsages { get; init; }
    /// <summary>超出上限时为 true，LLM 应知晓结果已截断</summary>
    public bool Truncated { get; init; }
    public List<UsageView> Usages { get; init; } = [];
}