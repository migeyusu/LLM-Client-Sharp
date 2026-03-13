namespace LLMClient.ContextEngineering.Tools.Models;

public sealed record SymbolBriefView : SymbolViewBase
{
    public LocationView? Location { get; init; }
}