namespace LLMClient.ContextEngineering.Tools.Models;

public sealed record TypeMembersView : SymbolViewBase
{
    public int TotalCount { get; init; }
    public List<MemberSummaryView> Members { get; init; } = [];
}