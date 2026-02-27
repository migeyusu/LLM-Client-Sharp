using System.Text.Json;
using LLMClient.ContextEngineering.Tools;
using LLMClient.ContextEngineering.Tools.Models;

namespace LLMClient.Test.Agent.Tools;

public class ProjectAwarenessPluginTests
{
    // ── 假服务 ───────────────────────────────────────────────────────────

    /// <summary>每个方法的返回值可单独配置，默认返回合理的测试数据</summary>
    private sealed class FakeService : IProjectAwarenessService
    {
        public SolutionInfoView SolutionInfoView { get; set; } = BuildDefaultSolutionView();
        public ProjectMetadataView? ProjectMetadataView { get; set; } = BuildDefaultMetadataView();
        public string FileTreeResult { get; set; } = "```\n└── Services\n    └── UserService.cs\n```";
        public FileMetadataView? FileMetadataView { get; set; } = BuildDefaultFileView();
        public ConventionView ConventionView { get; set; } = BuildDefaultConventionView();
        public List<RecentFileView> RecentFiles { get; set; } = BuildDefaultRecentFiles();

        // 可注入的异常（模拟服务层错误）
        public Exception? GetProjectMetadataException { get; set; }
        public Exception? GetFileMetadataException { get; set; }
        public Exception? GetSolutionInfoException { get; set; }

        public SolutionInfoView GetSolutionInfoView()
        {
            if (GetSolutionInfoException is not null) throw GetSolutionInfoException;
            return SolutionInfoView;
        }

        public ProjectMetadataView GetProjectMetadataView(string nameOrId)
        {
            if (GetProjectMetadataException is not null) throw GetProjectMetadataException;
            return ProjectMetadataView
                   ?? throw new ArgumentException($"Project not found: '{nameOrId}'.");
        }

        public string GetFileTree(string path, int maxDepth, ICollection<string>? excludePatterns)
            => FileTreeResult;

        public FileMetadataView GetFileMetadata(string path)
        {
            if (GetFileMetadataException is not null) throw GetFileMetadataException;
            return FileMetadataView
                   ?? throw new FileNotFoundException($"File not found: {path}");
        }

        public ConventionView DetectConventions() => ConventionView;

        public List<RecentFileView> GetRecentlyModifiedFiles(DateTime? sinceUtc = null, int count = 30)
            => RecentFiles.Take(count).ToList();

        // ── 默认假数据构建 ────────────────────────────────────────────────

        private static SolutionInfoView BuildDefaultSolutionView() => new()
        {
            SolutionName = "MyApp",
            SolutionPath = TestFixtures.SolutionPath,
            ProjectCount = 1,
            Frameworks   = new List<string> { "net9.0" },
            Conventions  = new ConventionView { UsesNullable = true },
            Projects = new List<ProjectBriefView>
            {
                new()
                {
                    ProjectId       = TestFixtures.CoreProjectPath,
                    Name            = "MyApp.Core",
                    ProjectFilePath = @"MyApp.Core\MyApp.Core.csproj",
                    OutputType      = "Library",
                    FilesCount      = 5
                }
            }
        };

        private static ProjectMetadataView BuildDefaultMetadataView() => new()
        {
            ProjectId       = TestFixtures.CoreProjectPath,
            Name            = "MyApp.Core",
            ProjectFilePath = @"MyApp.Core\MyApp.Core.csproj",
            FullRootDir     = @"C:\Projects\MyApp\MyApp.Core",
            Language        = "C#",
            LanguageVersion = "12.0",
            OutputType      = "Library",
            TargetFrameworks = new List<string> { "net9.0" },
            Conventions     = new ConventionView { UsesNullable = true },
            Statistics = new ProjectStatsView
            {
                FilesCount = 5, TypesCount = 5, MethodsCount = 12, LinesOfCode = 520
            }
        };

        private static FileMetadataView BuildDefaultFileView() => new()
        {
            FilePath         = @"C:\Projects\MyApp\MyApp.Core\Services\UserService.cs",
            RelativePath     = @"MyApp.Core\Services\UserService.cs",
            Extension        = ".cs",
            Kind             = "Source",
            SizeBytes        = 8000,
            LinesOfCode      = 200,
            LastWriteTimeUtc = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc)
        };

        private static ConventionView BuildDefaultConventionView() => new()
        {
            HasEditorConfig = true, UsesNullable = true, UsesImplicitUsings = true,
            TestFrameworkHint = "xUnit", DefaultNamespaceStyle = "MyApp"
        };

        private static List<RecentFileView> BuildDefaultRecentFiles() => new()
        {
            new RecentFileView
            {
                FilePath         = @"C:\Projects\MyApp\MyApp.Core\Services\UserService.cs",
                RelativePath     = @"MyApp.Core\Services\UserService.cs",
                LastWriteTimeUtc = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc),
                SizeBytes        = 8000,
                Kind             = "Source"
            }
        };
    }

    private static ProjectAwarenessPlugin CreatePlugin(FakeService? fake = null)
        => new(fake ?? new FakeService());

    // ── GetSolutionInfo ──────────────────────────────────────────────────

    [Fact]
    public void GetSolutionInfo_ReturnsValidJson()
    {
        var plugin = CreatePlugin();

        var json = plugin.GetSolutionInfo();

        var doc = JsonDocument.Parse(json);
        Assert.Equal("MyApp", doc.RootElement.GetProperty("solutionName").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("projectCount").GetInt32());
    }

    [Fact]
    public void GetSolutionInfo_ContainsProjectsList()
    {
        var plugin = CreatePlugin();

        var json   = plugin.GetSolutionInfo();
        var doc    = JsonDocument.Parse(json);
        var projects = doc.RootElement.GetProperty("projects");

        Assert.Equal(JsonValueKind.Array, projects.ValueKind);
        Assert.Equal(1, projects.GetArrayLength());
    }

    [Fact]
    public void GetSolutionInfo_ServiceThrowsInvalidOperation_ReturnsErrorJson()
    {
        var fake = new FakeService
        {
            GetSolutionInfoException = new InvalidOperationException("No solution loaded.")
        };
        var plugin = CreatePlugin(fake);

        var json = plugin.GetSolutionInfo();

        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
        Assert.Contains("No solution loaded.", doc.RootElement.GetProperty("error").GetString());
    }

    // ── GetProjectMetadata ───────────────────────────────────────────────

    [Fact]
    public void GetProjectMetadata_ValidId_ReturnsMetadataJson()
    {
        var plugin = CreatePlugin();

        var json = plugin.GetProjectMetadata("MyApp.Core");

        var doc = JsonDocument.Parse(json);
        Assert.Equal("MyApp.Core", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("Library",    doc.RootElement.GetProperty("outputType").GetString());
    }

    [Fact]
    public void GetProjectMetadata_ContainsStatistics()
    {
        var plugin = CreatePlugin();

        var json = plugin.GetProjectMetadata("MyApp.Core");

        var doc  = JsonDocument.Parse(json);
        var stat = doc.RootElement.GetProperty("statistics");
        Assert.Equal(5,  stat.GetProperty("filesCount").GetInt32());
        Assert.Equal(12, stat.GetProperty("methodsCount").GetInt32());
    }

    [Fact]
    public void GetProjectMetadata_InvalidId_ReturnsErrorJson()
    {
        var fake = new FakeService
        {
            GetProjectMetadataException = new ArgumentException("Project not found: 'Unknown'.")
        };
        var plugin = CreatePlugin(fake);

        var json = plugin.GetProjectMetadata("Unknown");

        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("error", out var err));
        Assert.Contains("not found", err.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    // ── GetFileTree ──────────────────────────────────────────────────────

    [Fact]
    public void GetFileTree_ReturnsServiceResult()
    {
        var expected = "```\n└── Services\n    └── UserService.cs\n```";
        var fake     = new FakeService { FileTreeResult = expected };
        var plugin   = CreatePlugin(fake);

        var result = plugin.GetFileTree();

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetFileTree_DefaultParameters_UseDotPath()
    {
        string? capturedPath = null;
        int capturedDepth    = -1;
        ICollection<string>? capturedPatterns = null;

        var fake = new FakeService();
        // 用 lambda 捕获（直接子类重写）
        var capturingSvc = new CapturingService(fake,
            (path, depth, patterns) => { capturedPath = path; capturedDepth = depth; capturedPatterns = patterns; });
        var plugin = new ProjectAwarenessPlugin(capturingSvc);

        plugin.GetFileTree();

        Assert.Equal(".", capturedPath);
        Assert.Equal(4,   capturedDepth);
        Assert.NotNull(capturedPatterns);
    }

    [Fact]
    public void GetFileTree_ExcludePatterns_ParsedFromCommaString()
    {
        ICollection<string>? capturedPatterns = null;
        var capturingSvc = new CapturingService(new FakeService(),
            (_, _, patterns) => capturedPatterns = patterns);
        var plugin = new ProjectAwarenessPlugin(capturingSvc);

        plugin.GetFileTree(excludePatterns: "obj,bin,.vs");

        Assert.NotNull(capturedPatterns);
        Assert.Contains("obj",  capturedPatterns!);
        Assert.Contains("bin",  capturedPatterns!);
        Assert.Contains(".vs",  capturedPatterns!);
        Assert.Equal(3, capturedPatterns!.Count);
    }

    [Fact]
    public void GetFileTree_ExcludePatterns_TrimsWhitespace()
    {
        ICollection<string>? capturedPatterns = null;
        var capturingSvc = new CapturingService(new FakeService(),
            (_, _, patterns) => capturedPatterns = patterns);
        var plugin = new ProjectAwarenessPlugin(capturingSvc);

        plugin.GetFileTree(excludePatterns: " obj , bin , .vs ");

        Assert.All(capturedPatterns!, p => Assert.DoesNotContain(" ", p));
    }

    // ── GetFileMetadata ──────────────────────────────────────────────────

    [Fact]
    public void GetFileMetadata_ValidPath_ReturnsMetadataJson()
    {
        var plugin = CreatePlugin();

        var json = plugin.GetFileMetadata(@"MyApp.Core\Services\UserService.cs");

        var doc = JsonDocument.Parse(json);
        Assert.Equal("Source", doc.RootElement.GetProperty("kind").GetString());
        Assert.Equal(200,      doc.RootElement.GetProperty("linesOfCode").GetInt32());
    }

    [Fact]
    public void GetFileMetadata_FileNotFound_ReturnsErrorJson()
    {
        var fake = new FakeService
        {
            GetFileMetadataException = new FileNotFoundException("File not found: Missing.cs")
        };
        var plugin = CreatePlugin(fake);

        var json = plugin.GetFileMetadata("Missing.cs");

        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    // ── DetectConventions ────────────────────────────────────────────────

    [Fact]
    public void DetectConventions_ReturnsConventionJson()
    {
        var plugin = CreatePlugin();

        var json = plugin.DetectConventions();

        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("hasEditorConfig").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("usesNullable").GetBoolean());
        Assert.Equal("xUnit", doc.RootElement.GetProperty("testFrameworkHint").GetString());
    }

    // ── GetRecentlyModifiedFiles ─────────────────────────────────────────

    [Fact]
    public void GetRecentlyModifiedFiles_DefaultCount_ReturnsJsonArray()
    {
        var plugin = CreatePlugin();

        var json = plugin.GetRecentlyModifiedFiles();

        var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public void GetRecentlyModifiedFiles_EachEntryHasRequiredFields()
    {
        var plugin = CreatePlugin();

        var json  = plugin.GetRecentlyModifiedFiles();
        var doc   = JsonDocument.Parse(json);
        var first = doc.RootElement.EnumerateArray().First();

        Assert.True(first.TryGetProperty("filePath",         out _));
        Assert.True(first.TryGetProperty("kind",             out _));
        Assert.True(first.TryGetProperty("lastWriteTimeUtc", out _));
    }

    [Fact]
    public void GetRecentlyModifiedFiles_NullSince_StillReturnsResults()
    {
        var plugin = CreatePlugin();

        // 不传 sinceUtc，不应抛出
        var json = plugin.GetRecentlyModifiedFiles(sinceUtc: null);

        var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    // ── JSON 通用约束 ────────────────────────────────────────────────────

    [Theory]
    [InlineData(nameof(ProjectAwarenessPlugin.GetSolutionInfo))]
    [InlineData(nameof(ProjectAwarenessPlugin.DetectConventions))]
    [InlineData(nameof(ProjectAwarenessPlugin.GetRecentlyModifiedFiles))]
    public void AllReadMethods_ReturnValidJsonString(string methodName)
    {
        var plugin = CreatePlugin();
        var method = typeof(ProjectAwarenessPlugin).GetMethod(methodName)!;

        var result = (string)method.Invoke(plugin, method.GetParameters()
            .Select(_ => (object?)null).ToArray())!;

        // 不抛出即为合法 JSON
        var ex = Record.Exception(() => JsonDocument.Parse(result));
        Assert.Null(ex);
    }

    // ── 捕获服务（测试参数传递）─────────────────────────────────────────

    private sealed class CapturingService : IProjectAwarenessService
    {
        private readonly FakeService _inner;
        private readonly Action<string, int, ICollection<string>?> _onGetFileTree;

        public CapturingService(FakeService inner, Action<string, int, ICollection<string>?> onGetFileTree)
        {
            _inner         = inner;
            _onGetFileTree = onGetFileTree;
        }

        public SolutionInfoView GetSolutionInfoView()        => _inner.GetSolutionInfoView();
        public ProjectMetadataView GetProjectMetadataView(string id) => _inner.GetProjectMetadataView(id);
        public FileMetadataView GetFileMetadata(string path)=> _inner.GetFileMetadata(path);
        public ConventionView DetectConventions()            => _inner.DetectConventions();
        public List<RecentFileView> GetRecentlyModifiedFiles(DateTime? s, int c) => _inner.GetRecentlyModifiedFiles(s, c);

        public string GetFileTree(string path, int maxDepth, ICollection<string>? patterns)
        {
            _onGetFileTree(path, maxDepth, patterns);
            return _inner.GetFileTree(path, maxDepth, patterns);
        }
    }
}