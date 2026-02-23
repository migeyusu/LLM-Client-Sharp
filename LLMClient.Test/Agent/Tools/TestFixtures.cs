using LLMClient.ContextEngineering.Analysis;

namespace LLMClient.Test.Agent.Tools;

/// <summary>
/// 构建用于测试的 SolutionInfo / ProjectInfo 假数据，
/// 所有路径使用 Windows 格式（跨平台时可替换为 Path.Combine）
/// </summary>
internal static class TestFixtures
{
    public const string SolutionDir = @"C:\Projects\MyApp";
    public const string SolutionPath = @"C:\Projects\MyApp\MyApp.sln";

    // ── Project 路径常量 ─────────────────────────────────────────────────

    public const string CoreProjectPath = @"C:\Projects\MyApp\MyApp.Core\MyApp.Core.csproj";
    public const string ApiProjectPath  = @"C:\Projects\MyApp\MyApp.Api\MyApp.Api.csproj";

    // ── Solution 构建 ────────────────────────────────────────────────────

    public static SolutionInfo BuildSolution(params ProjectInfo[] projects)
    {
        var s = new SolutionInfo
        {
            SolutionName = "MyApp",
            SolutionPath = SolutionPath,
            GeneratedAt  = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Conventions  = new ConventionInfo
            {
                HasEditorConfig     = true,
                EditorConfigPath    = @"C:\Projects\MyApp\.editorconfig",
                UsesNullable        = true,
                UsesImplicitUsings  = true,
                DefaultNamespaceStyle = "MyApp",
                TestFrameworkHint   = "xUnit",
                NotableFiles        = new List<string> { @"C:\Projects\MyApp\README.md" }
            }
        };
        s.Projects.AddRange(projects.Length > 0 ? projects : new[] { BuildCoreProject() });
        return s;
    }

    // ── Project 构建 ─────────────────────────────────────────────────────

    public static ProjectInfo BuildCoreProject(
        IEnumerable<FileEntryInfo>? files = null,
        DateTime? lastModifiedOverride = null)
    {
        var baseTime = lastModifiedOverride ?? new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);

        var p = new ProjectInfo
        {
            Name            = "MyApp.Core",
            ProjectFilePath = CoreProjectPath,
            RelativeRootDir = @"MyApp.Core\MyApp.Core.csproj",
            FullRootDir     = @"C:\Projects\MyApp\MyApp.Core",
            OutputType      = "Library",
            Language        = "C#",
            LanguageVersion = "12.0",
            Conventions = new ConventionInfo
            {
                HasEditorConfig     = true,
                UsesNullable        = true,
                UsesImplicitUsings  = true,
                TestFrameworkHint   = "Unknown",
                DefaultNamespaceStyle = "MyApp.Core"
            }
        };
        p.TargetFrameworks.Add("net9.0");
        p.PackageReferences.AddRange(new[]
        {
            new PackageReference { Name = "Microsoft.Extensions.DependencyInjection", Version = "9.0.0" },
            new PackageReference { Name = "Newtonsoft.Json", Version = "13.0.3" }
        });

        // 文件索引
        p.Files.AddRange(files ?? new[]
        {
            BuildFile(@"C:\Projects\MyApp\MyApp.Core", @"Services\UserService.cs",     "Source",  200, baseTime),
            BuildFile(@"C:\Projects\MyApp\MyApp.Core", @"Services\OrderService.cs",    "Source",  150, baseTime.AddDays(-1)),
            BuildFile(@"C:\Projects\MyApp\MyApp.Core", @"Models\User.cs",              "Source",   80, baseTime.AddDays(-2)),
            BuildFile(@"C:\Projects\MyApp\MyApp.Core", @"Models\Order.cs",             "Source",   60, baseTime.AddDays(-3)),
            BuildFile(@"C:\Projects\MyApp\MyApp.Core", @"Interfaces\IUserService.cs",  "Source",   30, baseTime.AddDays(-4)),
        });

        p.Statistics.FilesCount  = p.Files.Count;
        p.Statistics.TypesCount  = 5;
        p.Statistics.MethodsCount = 12;
        p.Statistics.LinesOfCode = p.Files.Sum(f => f.LinesOfCode);

        // 至少一个 namespace/type，供 Fallback 路径测试
        var ns = new NamespaceInfo { Name = "MyApp.Core.Services", FilePath = CoreProjectPath };
        ns.Types.Add(new TypeInfo
        {
            Name          = "UserService",
            Kind          = "Class",
            Signature     = "public class UserService",
            Accessibility = "Public",
            FilePath      = @"C:\Projects\MyApp\MyApp.Core\Services\UserService.cs",
            RelativePath  = @"Services\UserService.cs",
            LineNumber    = 1,
            Locations     = new List<CodeLocation>()
        });
        p.Namespaces.Add(ns);

        return p;
    }

    public static ProjectInfo BuildApiProject()
    {
        var p = new ProjectInfo
        {
            Name            = "MyApp.Api",
            ProjectFilePath = ApiProjectPath,
            RelativeRootDir = @"MyApp.Api\MyApp.Api.csproj",
            FullRootDir     = @"C:\Projects\MyApp\MyApp.Api",
            OutputType      = "Exe",
            Language        = "C#",
            LanguageVersion = "12.0",
            Conventions     = new ConventionInfo { UsesNullable = true, TestFrameworkHint = "Unknown" }
        };
        p.TargetFrameworks.Add("net9.0");
        p.ProjectReferences.Add(new ProjectReference
        {
            ProjectName = "MyApp.Core",
            ProjectPath = CoreProjectPath
        });
        p.Files.Add(BuildFile(@"C:\Projects\MyApp\MyApp.Api", @"Controllers\UserController.cs", "Source", 120,
            new DateTime(2025, 6, 2, 9, 0, 0, DateTimeKind.Utc)));

        p.Statistics.FilesCount   = p.Files.Count;
        p.Statistics.TypesCount   = 1;
        p.Statistics.MethodsCount = 4;
        p.Statistics.LinesOfCode  = p.Files.Sum(f => f.LinesOfCode);
        return p;
    }

    // ── 文件条目构建 ─────────────────────────────────────────────────────

    public static FileEntryInfo BuildFile(
        string projectRoot,
        string relativeFromProjectRoot,
        string kind,
        int lines,
        DateTime lastWrite)
    {
        var absPath = Path.Combine(projectRoot, relativeFromProjectRoot);
        return new FileEntryInfo
        {
            FilePath         = absPath,
            RelativePath     = relativeFromProjectRoot,
            ProjectFilePath  = Path.Combine(projectRoot, Path.GetFileName(projectRoot) + ".csproj"),
            Extension        = Path.GetExtension(absPath),
            SizeBytes        = lines * 40L,  // rough estimate
            LinesOfCode      = lines,
            LastWriteTimeUtc = lastWrite,
            Kind             = kind
        };
    }
}