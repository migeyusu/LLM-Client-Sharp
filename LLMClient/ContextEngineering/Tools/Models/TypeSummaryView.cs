namespace LLMClient.ContextEngineering.Tools.Models;

public sealed record TypeSummaryView : DescribedSymbolViewBase
{
    public int MemberCount { get; init; }
    public LocationView? Location { get; init; }
}