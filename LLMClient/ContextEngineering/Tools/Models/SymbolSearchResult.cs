namespace LLMClient.ContextEngineering.Tools.Models;

// ── search_symbols ────────────────────────────────────────────────────────
public sealed record SymbolSearchResult : MultiLocatableDescribedSymbolViewBase
{
    // SymbolId, Name, Kind, Signature, Accessibility, Summary inherited
    public string? ContainingType { get; init; }
    public string? ContainingNamespace { get; init; }
    public double Score { get; init; }
}