namespace LLMClient.ContextEngineering.Tools.Models;

public sealed record CallersView : SymbolViewBase
{
    public int TotalCallers { get; init; }
    public List<CallerView> Callers { get; init; } = [];
}