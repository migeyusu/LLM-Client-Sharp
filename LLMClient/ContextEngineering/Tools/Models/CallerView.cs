namespace LLMClient.ContextEngineering.Tools.Models;

public sealed record CallerView
{
    public string CallerSymbolId { get; init; } = "";
    public string CallerName { get; init; } = "";
    public string CallerSignature { get; init; } = "";
    public List<LocationView> CallSites { get; init; } = [];
}