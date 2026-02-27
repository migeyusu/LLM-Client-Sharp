namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class CallersView
{
    public string SymbolId { get; init; } = "";
    public string SymbolName { get; init; } = "";
    public int TotalCallers { get; init; }
    public List<CallerView> Callers { get; init; } = [];
}