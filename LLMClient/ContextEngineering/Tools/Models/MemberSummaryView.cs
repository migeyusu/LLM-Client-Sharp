namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class MemberSummaryView
{
    public string SymbolId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Signature { get; init; } = "";
    public string Accessibility { get; init; } = "";
    public string? ReturnType { get; init; }
    public string? Summary { get; init; }
    public bool IsStatic { get; init; }
    public bool IsAsync { get; init; }
    public LocationView? Location { get; init; }
}