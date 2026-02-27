namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class TypeMembersView
{
    public string TypeId { get; init; } = "";
    public string TypeName { get; init; } = "";
    public string TypeSignature { get; init; } = "";
    public int TotalCount { get; init; }
    public List<MemberSummaryView> Members { get; init; } = [];
}