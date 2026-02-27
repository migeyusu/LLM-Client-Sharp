namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class ProjectMetadataView
{
    public required string ProjectId { get; set; }
    public required string Name { get; set; }
    public required string ProjectFilePath { get; set; }
    public required string FullRootDir { get; set; }

    public string? Language { get; set; }
    public string? LanguageVersion { get; set; }

    public required string OutputType { get; set; }
    public List<string> TargetFrameworks { get; set; } = new();

    public List<ProjectReferenceView> ProjectReferences { get; set; } = new();
    public List<PackageReferenceView> PackageReferences { get; set; } = new();

    public ConventionView Conventions { get; set; } = new();

    public ProjectStatsView Statistics { get; set; } = new();
}