namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class ImplementationsView
{
    public string InterfaceId { get; init; } = "";
    public string InterfaceName { get; init; } = "";
    public List<SymbolBriefView> Implementations { get; init; } = [];
    public string Source { get; init; } = "";
}