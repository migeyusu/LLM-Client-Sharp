namespace LLMClient.ContextEngineering.Tools.Models;

public sealed record SymbolDetailView : MultiLocatableDescribedSymbolViewBase
{
    public List<string> Attributes { get; init; } = [];
    public TypeDetailExtra? TypeDetail { get; set; }
    public MemberDetailExtra? MemberDetail { get; set; }
}