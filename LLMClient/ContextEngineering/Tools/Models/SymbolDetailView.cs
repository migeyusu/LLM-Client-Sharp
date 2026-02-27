namespace LLMClient.ContextEngineering.Tools.Models;

public sealed record SymbolDetailView
{
    public string SymbolId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Signature { get; init; } = "";
    public string Accessibility { get; init; } = "";
    public string? Summary { get; init; }
    public List<string> Attributes { get; init; } = [];
    public List<LocationView> Locations { get; set; } = [];
    public TypeDetailExtra? TypeDetail { get; set; }
    public MemberDetailExtra? MemberDetail { get; set; }
}