namespace LLMClient.ContextEngineering.Tools.Models;

public sealed record ProjectReferenceView
{
    public required string ProjectName { get; set; }
    public required string ProjectPath { get; set; }
}