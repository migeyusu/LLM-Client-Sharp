using System.Text.Json.Serialization;

namespace LLMClient.ContextEngineering.Analysis;

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
    
    /// <summary>
    /// 项目内文件索引（项目感知工具需要）
    /// </summary>
    public List<FileEntryInfo> Files { get; set; } = new();
    
    /// <summary>
    /// 项目约定/配置探测结果（项目感知工具需要）
    /// </summary>
    public ConventionInfo Conventions { get; set; } = new();
    
    /// <summary>
    /// 供工具层识别项目：优先使用 ProjectFilePath 作为稳定 id
    /// </summary>
    public string ProjectId => ProjectFilePath;
}