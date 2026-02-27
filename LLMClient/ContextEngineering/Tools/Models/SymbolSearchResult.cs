namespace LLMClient.ContextEngineering.Tools.Models;

// ── search_symbols ────────────────────────────────────────────────────────
public sealed class SymbolSearchResult
{
    public string SymbolId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Signature { get; init; } = "";
    public string Accessibility { get; init; } = "";
    public string? ContainingType { get; init; }
    public string? ContainingNamespace { get; init; }
    public string? Summary { get; init; }
    public List<LocationView> Locations { get; init; } = [];
    public double Score { get; init; }
}