namespace LLMClient.ContextEngineering.Tools.Models;

public sealed record MemberSummaryView : DescribedSymbolViewBase
{
    public string? ReturnType { get; init; }
    public bool IsStatic { get; init; }
    public bool IsAsync { get; init; }
    public LocationView? Location { get; init; }
}