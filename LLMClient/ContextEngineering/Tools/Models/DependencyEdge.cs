namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class DependencyEdge
{
    public string From { get; init; } = "";
    public string To { get; init; } = "";
    /// <summary>ProjectReference | PackageReference</summary>
    public string Kind { get; init; } = "";
}