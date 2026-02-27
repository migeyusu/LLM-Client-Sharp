namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class TypeSummaryView
{
    public string SymbolId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Signature { get; init; } = "";
    public string Accessibility { get; init; } = "";
    public string? Summary { get; init; }
    public int MemberCount { get; init; }
    public LocationView? Location { get; init; }
}