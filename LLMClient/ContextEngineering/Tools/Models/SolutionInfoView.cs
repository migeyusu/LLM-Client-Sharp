namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class SolutionInfoView
{
    public required string SolutionName { get; set; }
    public required string SolutionPath { get; set; }

    public int ProjectCount { get; set; }

    public List<ProjectBriefView> Projects { get; set; } = new();

    public List<string> Frameworks { get; set; } = new();

    public ConventionView Conventions { get; set; } = new();
}