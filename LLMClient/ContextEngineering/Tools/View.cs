namespace LLMClient.ContextEngineering.Tools;

public sealed class SolutionInfoView
{
    public required string SolutionName { get; set; }
    public required string SolutionPath { get; set; }

    public int ProjectCount { get; set; }

    public List<ProjectBriefView> Projects { get; set; } = new();

    public List<string> Frameworks { get; set; } = new();

    public ConventionView Conventions { get; set; } = new();
}

public sealed class ProjectBriefView
{
    public required string ProjectId { get; set; } // = ProjectFilePath
    public required string Name { get; set; }
    public required string ProjectFilePath { get; set; }

    public required string OutputType { get; set; }
    public List<string> TargetFrameworks { get; set; } = new();

    public int FilesCount { get; set; }
}

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

public sealed class PackageReferenceView
{
    public required string Name { get; set; }
    public required string Version { get; set; }
}

public sealed class ProjectReferenceView
{
    public required string ProjectName { get; set; }
    public required string ProjectPath { get; set; }
}

public sealed class ProjectStatsView
{
    public int FilesCount { get; set; }
    public int TypesCount { get; set; }
    public int MethodsCount { get; set; }
    public int LinesOfCode { get; set; }
}

public sealed class ConventionView
{
    public bool HasEditorConfig { get; set; }
    public string? EditorConfigPath { get; set; }

    public bool UsesNullable { get; set; }
    public bool UsesImplicitUsings { get; set; }

    public string? DefaultNamespaceStyle { get; set; }
    public string? TestFrameworkHint { get; set; }

    public List<string> NotableFiles { get; set; } = new();
}

public sealed class FileMetadataView
{
    public required string FilePath { get; set; }
    public string? RelativePath { get; set; }
    public string Extension { get; set; } = string.Empty;
    public string Kind { get; set; } = "Other";

    public long SizeBytes { get; set; }
    public int LinesOfCode { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
}

public sealed class RecentFileView
{
    public required string FilePath { get; set; }
    public string? RelativePath { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
    public long SizeBytes { get; set; }
    public string Kind { get; set; } = "Other";
}

public sealed class FileTreeView
{
    public required string Root { get; set; }
    public int Depth { get; set; }
    public List<FileTreeNodeView> Nodes { get; set; } = new();
}

public sealed class FileTreeNodeView
{
    public required string Name { get; set; }
    public required string Path { get; set; } // absolute
    public bool IsFile { get; set; }
    public List<FileTreeNodeView> Children { get; set; } = new();
}

public sealed class GetFileTreeArgs
{
    public required string RootPath { get; set; }
    public int Depth { get; set; } = 4;
    public List<string>? ExcludePatterns { get; set; }
}

public sealed class GetRecentlyModifiedFilesArgs
{
    public DateTime? SinceUtc { get; set; }
    public int Count { get; set; } = 30;
}
