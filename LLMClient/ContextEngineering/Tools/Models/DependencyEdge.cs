namespace LLMClient.ContextEngineering.Tools.Models;

public sealed record DependencyEdge
{
    public string From { get; init; } = "";
    public string To { get; init; } = "";
    /// <summary>ProjectReference | PackageReference</summary>
    public string Kind { get; init; } = "";
}