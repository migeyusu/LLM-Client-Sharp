namespace LLMClient.ContextEngineering.Tools.Models;

public sealed record PackageReferenceView
{
    public required string Name { get; set; }
    public required string Version { get; set; }
}