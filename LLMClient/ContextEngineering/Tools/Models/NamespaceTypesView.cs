namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class NamespaceTypesView
{
    public string Namespace { get; init; } = "";
    public bool IncludesSubNamespaces { get; init; }
    public int TotalCount { get; init; }
    public List<TypeSummaryView> Types { get; init; } = [];
}