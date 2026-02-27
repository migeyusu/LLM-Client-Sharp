namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class CalleesView
{
    public string SymbolId { get; init; } = "";
    public string SymbolName { get; init; } = "";
    public List<SymbolBriefView> Callees { get; init; } = [];
}