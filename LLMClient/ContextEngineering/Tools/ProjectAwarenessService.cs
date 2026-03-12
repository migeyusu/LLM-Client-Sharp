using LLMClient.ContextEngineering.Analysis;
using LLMClient.ContextEngineering.PromptGeneration;
using LLMClient.ContextEngineering.Tools.Models;

namespace LLMClient.ContextEngineering.Tools;

internal sealed class ProjectAwarenessService : IProjectAwarenessService
{
    private readonly FileTreeFormatter _formatter = new();
    private readonly SolutionContext _context;

    public ProjectAwarenessService(SolutionContext context)
    {
        _context = context;
    }

    // ── get_solution_info ────────────────────────────────────────────────

    public SolutionInfoView GetSolutionInfoView()
    {
        var s = _context.RequireSolutionInfoOrThrow();
        return new SolutionInfoView
        {
            SolutionName = s.SolutionName,
            SolutionPath = s.SolutionPath,
            ProjectCount = s.Projects.Count,
            Frameworks = s.Projects
                .SelectMany(p => p.TargetFrameworks)
                .Distinct()
                .OrderBy(x => x)
                .ToList(),
            Conventions = Map(s.Conventions),
            Projects = s.Projects
                .OrderBy(p => p.Name)
                .Select(p => new ProjectBriefView
                {
                    ProjectId = p.ProjectId,
                    Name = p.Name,
                    // 输出相对路径，比绝对路径节省 tokens
                    ProjectFilePath = _context.ToSolutionRelative(p.ProjectFilePath),
                    OutputType = p.OutputType,
                    TargetFrameworks = p.TargetFrameworks.ToList(),
                    FilesCount = p.Files.Count
                })
                .ToList()
        };
    }

    // ── get_project_metadata（接受 Name 或 ProjectFilePath）──────────────

    public ProjectMetadataView GetProjectMetadataView(string nameOrId)
    {
        var s = _context.RequireSolutionInfoOrThrow();
        var solutionDir = _context.RequireSolutionDirOrThrow();

        // 同时支持项目名称和路径（相对/绝对均可）
        var resolvedId = Path.IsPathRooted(nameOrId)
            ? nameOrId
            : File.Exists(Path.Combine(solutionDir, nameOrId))
                ? Path.GetFullPath(Path.Combine(solutionDir, nameOrId))
                : nameOrId;

        var p = s.Projects.FirstOrDefault(x =>
                    string.Equals(x.Name, resolvedId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.ProjectId, resolvedId, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException(
                    $"Project not found: '{nameOrId}'. " +
                    $"Use get_solution_info to list valid project names.");

        return new ProjectMetadataView
        {
            ProjectId = p.ProjectId,
            Name = p.Name,
            ProjectFilePath = _context.ToSolutionRelative(p.ProjectFilePath),
            FullRootDir = p.FullRootDir,
            Language = p.Language,
            LanguageVersion = p.LanguageVersion,
            OutputType = p.OutputType,
            TargetFrameworks = p.TargetFrameworks.ToList(),
            ProjectReferences = p.ProjectReferences
                .Select(r => new ProjectReferenceView
                {
                    ProjectName = r.ProjectName,
                    ProjectPath = _context.ToSolutionRelative(r.ProjectPath)
                })
                .ToList(),
            PackageReferences = p.PackageReferences
                .Select(r => new PackageReferenceView { Name = r.Name, Version = r.Version })
                .ToList(),
            Conventions = Map(p.Conventions),
            Statistics = new ProjectStatsView
            {
                FilesCount = p.Statistics.FilesCount,
                TypesCount = p.Statistics.TypesCount,
                MethodsCount = p.Statistics.MethodsCount,
                LinesOfCode = p.Statistics.LinesOfCode
            }
        };
    }

    // ── get_file_tree ────────────────────────────────────────────────────

    /// <summary>
    /// 返回 ASCII 文件树（Markdown 代码块）。
    /// 只包含 Roslyn 已分析的项目文件，不做裸文件系统遍历。
    /// </summary>
    public string GetFileTree(string relativeRootPath, int maxDepth, ICollection<string>? excludePatterns)
    {
        var s = _context.RequireSolutionInfoOrThrow();
        var absRoot = _context.ResolveToAbsolute(relativeRootPath);
        var solutionDir = _context.RequireSolutionDirOrThrow();

        if (!Directory.Exists(absRoot))
            return $"> Error: path not found — `{relativeRootPath}`";

        // 从 Files 索引取数据，只取落在目标目录下的文件
        var paths = s.Projects
            .SelectMany(p => p.Files)
            .Where(f => f.FilePath.StartsWith(absRoot, StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(absRoot, f.FilePath))
            .Where(p => !string.IsNullOrWhiteSpace(p) && p != ".");

        var isRoot = string.Equals(absRoot, solutionDir, StringComparison.OrdinalIgnoreCase);
        var header = isRoot
            ? $"# Solution: {s.SolutionName}"
            : $"# Tree: `{relativeRootPath}`";

        return _formatter.Format(header, paths, maxDepth, excludePatterns);
    }

    // ── get_file_metadata ────────────────────────────────────────────────

    public FileMetadataView GetFileMetadata(string pathInput)
    {
        var s = _context.RequireSolutionInfoOrThrow();
        var absPath = _context.ResolveToAbsolute(pathInput);

        var entry = s.Projects
            .SelectMany(p => p.Files)
            .FirstOrDefault(f => string.Equals(f.FilePath, absPath, StringComparison.OrdinalIgnoreCase));

        if (entry != null)
        {
            return new FileMetadataView
            {
                FilePath = entry.FilePath,
                RelativePath = _context.ToSolutionRelative(entry.FilePath), // 统一相对 solution root
                Extension = entry.Extension,
                Kind = entry.Kind,
                SizeBytes = entry.SizeBytes,
                LinesOfCode = entry.LinesOfCode,
                LastWriteTimeUtc = entry.LastWriteTimeUtc
            };
        }

        // Fallback：不在索引中的文件（如 README.md）
        var fi = new FileInfo(absPath);
        if (!fi.Exists)
            throw new FileNotFoundException($"File not found: {pathInput}");

        return new FileMetadataView
        {
            FilePath = fi.FullName,
            RelativePath = _context.ToSolutionRelative(fi.FullName),
            Extension = fi.Extension,
            Kind = "Other",
            SizeBytes = fi.Length,
            LinesOfCode = 0,
            LastWriteTimeUtc = fi.LastWriteTimeUtc
        };
    }

    // ── detect_conventions ───────────────────────────────────────────────

    public ConventionView DetectConventions()
        => Map(_context.RequireSolutionInfoOrThrow().Conventions);

    // ── get_recently_modified_files ──────────────────────────────────────

    public List<RecentFileView> GetRecentlyModifiedFiles(DateTime? sinceUtc = null, int count = 30)
    {
        var s = _context.RequireSolutionInfoOrThrow();
        var q = s.Projects.SelectMany(p => p.Files).AsEnumerable();

        if (sinceUtc.HasValue)
            q = q.Where(f => f.LastWriteTimeUtc >= sinceUtc.Value);

        return q.OrderByDescending(f => f.LastWriteTimeUtc)
            .Take(Math.Clamp(count, 1, 200))
            .Select(f => new RecentFileView
            {
                FilePath = f.FilePath,
                RelativePath = _context.ToSolutionRelative(f.FilePath),
                LastWriteTimeUtc = f.LastWriteTimeUtc,
                SizeBytes = f.SizeBytes,
                Kind = f.Kind
            })
            .ToList();
    }

    // ── 映射工具 ────────────────────────────────────────────────────────

    private static ConventionView Map(ConventionInfo c) => new()
    {
        HasEditorConfig = c.HasEditorConfig,
        EditorConfigPath = c.EditorConfigPath,
        UsesNullable = c.UsesNullable,
        UsesImplicitUsings = c.UsesImplicitUsings,
        DefaultNamespaceStyle = c.DefaultNamespaceStyle,
        TestFrameworkHint = c.TestFrameworkHint,
        NotableFiles = c.NotableFiles.ToList()
    };
}