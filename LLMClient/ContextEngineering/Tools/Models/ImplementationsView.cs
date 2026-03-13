namespace LLMClient.ContextEngineering.Tools.Models;

public sealed record ImplementationsView : SourcedSymbolViewBase
{
    public List<SymbolBriefView> Implementations { get; init; } = [];
}