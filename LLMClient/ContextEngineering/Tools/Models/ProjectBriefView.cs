namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class ProjectBriefView
{
    public required string ProjectId { get; set; } // = ProjectFilePath
    public required string Name { get; set; }
    public required string ProjectFilePath { get; set; }

    public required string OutputType { get; set; }
    public List<string> TargetFrameworks { get; set; } = new();

    public int FilesCount { get; set; }
}