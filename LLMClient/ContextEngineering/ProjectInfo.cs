using System.Text.Json.Serialization;

namespace LLMClient.ContextEngineering;

public class ProjectInfo
{
    public required string Name { get; set; }
    public required string ProjectFilePath { get; set; }
    
    public required string RelativeRootDir { get; set; }

    public required string FullRootDir { get; set; }

    public required string OutputType { get; set; }
    public List<string> TargetFrameworks { get; set; } = new();

    public string? Language { get; set; }
    public string? LanguageVersion { get; set; }

    public List<PackageReference> PackageReferences { get; set; } = new();
    public List<ProjectReference> ProjectReferences { get; set; } = new();
    public List<NamespaceInfo> Namespaces { get; set; } = new();
    public ProjectStatistics Statistics { get; set; } = new();

    public DateTime GeneratedAt { get; set; }

    public string Version { get; set; } = "1.0";

    [JsonIgnore] public TimeSpan GenerationTime { get; set; }
}