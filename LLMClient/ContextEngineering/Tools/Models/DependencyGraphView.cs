namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class DependencyGraphView
{
    public string ScopeLabel { get; init; } = "";
    public List<DependencyNode> Nodes { get; init; } = [];
    public List<DependencyEdge> Edges { get; init; } = [];
}