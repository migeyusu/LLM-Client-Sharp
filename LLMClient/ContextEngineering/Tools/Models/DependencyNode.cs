namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class DependencyNode
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    /// <summary>Project | Package</summary>
    public string Kind { get; init; } = "";
}