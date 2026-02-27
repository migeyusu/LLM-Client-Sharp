namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class SymbolBriefView
{
    public string SymbolId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Signature { get; init; } = "";
    public LocationView? Location { get; init; }
}