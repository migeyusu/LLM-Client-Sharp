namespace LLMClient.ContextEngineering.Tools.Models;

public sealed record CalleesView : SymbolViewBase
{
    public List<SymbolBriefView> Callees { get; init; } = [];
}