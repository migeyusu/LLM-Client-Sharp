using LLMClient.ContextEngineering.Analysis;
using LLMClient.ContextEngineering.Tools;

namespace LLMClient.Test.Agent.Tools;

/// <summary>
/// 通过 SetCurrentForTesting 注入预构建 SolutionInfo，
/// 无需 MSBuild 工作区，完全内存中运行。
/// </summary>
public class ProjectAwarenessServiceTests
{
    private static ProjectAwarenessService CreateService(SolutionInfo solution)
    {
        var solutionContext = new SolutionContext(null!); // analyzer 不会被调用
        solutionContext.SetForTesting(solution);
        var svc = new ProjectAwarenessService(solutionContext);
        return svc;
    }

    // ── RequireSolution 守卫 ─────────────────────────────────────────────

    [Fact]
    public void GetSolutionInfo_WhenNotLoaded_ThrowsInvalidOperation()
    {
        var svc = new ProjectAwarenessService(null!);

        Assert.Throws<InvalidOperationException>(() => svc.GetSolutionInfoView());
    }

    // ── GetSolutionInfoView ──────────────────────────────────────────────

    [Fact]
    public void GetSolutionInfoView_ReturnsCorrectProjectCount()
    {
        var svc = CreateService(TestFixtures.BuildSolution(
            TestFixtures.BuildCoreProject(),
            TestFixtures.BuildApiProject()));

        var view = svc.GetSolutionInfoView();

        Assert.Equal(2, view.ProjectCount);
        Assert.Equal(2, view.Projects.Count);
    }

    [Fact]
    public void GetSolutionInfoView_ProjectFilePaths_AreRelativeToSolutionDir()
    {
        var svc = CreateService(TestFixtures.BuildSolution());
        var view = svc.GetSolutionInfoView();

        foreach (var project in view.Projects)
        {
            Assert.False(Path.IsPathRooted(project.ProjectFilePath),
                $"'{project.ProjectFilePath}' should be relative");
        }
    }

    [Fact]
    public void GetSolutionInfoView_DeduplicatesFrameworks()
    {
        var p1 = TestFixtures.BuildCoreProject();
        var p2 = TestFixtures.BuildApiProject();
        // 两个项目都有 net9.0
        var svc = CreateService(TestFixtures.BuildSolution(p1, p2));
        var view = svc.GetSolutionInfoView();

        Assert.Equal(view.Frameworks.Distinct().Count(), view.Frameworks.Count);
    }

    [Fact]
    public void GetSolutionInfoView_MapsConventionsCorrectly()
    {
        var svc = CreateService(TestFixtures.BuildSolution());
        var view = svc.GetSolutionInfoView();

        Assert.True(view.Conventions.HasEditorConfig);
        Assert.True(view.Conventions.UsesNullable);
        Assert.Equal("xUnit", view.Conventions.TestFrameworkHint);
    }

    // ── GetProjectMetadataView ───────────────────────────────────────────

    [Fact]
    public void GetProjectMetadataView_ByName_ReturnsCorrectProject()
    {
        var svc = CreateService(TestFixtures.BuildSolution());
        var view = svc.GetProjectMetadataView("MyApp.Core");

        Assert.Equal("MyApp.Core", view.Name);
        Assert.Equal("Library", view.OutputType);
    }

    [Fact]
    public void GetProjectMetadataView_ByRelativeCsprojPath_ReturnsCorrectProject()
    {
        var svc = CreateService(TestFixtures.BuildSolution());
        // 相对 solution 根目录的 .csproj 路径
        var relPath = Path.GetRelativePath(TestFixtures.SolutionDir, TestFixtures.CoreProjectPath);

        var view = svc.GetProjectMetadataView(relPath);

        Assert.Equal("MyApp.Core", view.Name);
    }

    [Fact]
    public void GetProjectMetadataView_ByAbsolutePath_ReturnsCorrectProject()
    {
        var svc = CreateService(TestFixtures.BuildSolution());
        var view = svc.GetProjectMetadataView(TestFixtures.CoreProjectPath);

        Assert.Equal("MyApp.Core", view.Name);
    }

    [Fact]
    public void GetProjectMetadataView_UnknownId_ThrowsArgumentException()
    {
        var svc = CreateService(TestFixtures.BuildSolution());

        Assert.Throws<ArgumentException>(() => svc.GetProjectMetadataView("DoesNotExist"));
    }

    [Fact]
    public void GetProjectMetadataView_ProjectFilePath_IsRelativeToSolutionDir()
    {
        var svc = CreateService(TestFixtures.BuildSolution());
        var view = svc.GetProjectMetadataView("MyApp.Core");

        Assert.False(Path.IsPathRooted(view.ProjectFilePath),
            $"'{view.ProjectFilePath}' should be relative");
    }

    [Fact]
    public void GetProjectMetadataView_PackageReferences_AreMapped()
    {
        var svc = CreateService(TestFixtures.BuildSolution());
        var view = svc.GetProjectMetadataView("MyApp.Core");

        Assert.Contains(view.PackageReferences, p => p.Name == "Newtonsoft.Json");
    }

    // ── GetFileTree ──────────────────────────────────────────────────────

    [Fact]
    public void GetFileTree_DotPath_IncludesAllProjectFiles()
    {
        var svc = CreateService(TestFixtures.BuildSolution());
        var result = svc.GetFileTree(".", 4, null);

        Assert.Contains("UserService.cs", result);
        Assert.Contains("OrderService.cs", result);
    }

    [Fact]
    public void GetFileTree_SubdirectoryPath_OnlyIncludesFilesUnderIt()
    {
        var solution = TestFixtures.BuildSolution(TestFixtures.BuildCoreProject());
        var svc = CreateService(solution);

        // 只看 Services 目录
        var result = svc.GetFileTree(@"MyApp.Core\Services", 4, null);

        Assert.Contains("UserService.cs", result);
        Assert.DoesNotContain("User.cs", result); // Models 下的文件不应出现
    }

    [Fact]
    public void GetFileTree_PathNotFound_ReturnsErrorMessage()
    {
        var svc = CreateService(TestFixtures.BuildSolution());
        var result = svc.GetFileTree("NonExistentFolder", 4, null);
        Assert.Contains("Error", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetFileTree_WithExcludePatterns_FiltersResults()
    {
        var project = TestFixtures.BuildCoreProject();
        project.Files.Add(TestFixtures.BuildFile(
            project.FullRootDir, @"obj\Debug\Build.cs", "Generated", 10,
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        var svc = CreateService(TestFixtures.BuildSolution(project));
        var result = svc.GetFileTree(".", 4, new[] { "obj" });

        Assert.DoesNotContain("Build.cs", result);
    }

    // ── GetFileMetadata ──────────────────────────────────────────────────

    [Fact]
    public void GetFileMetadata_IndexedFile_ReturnsFromIndex()
    {
        var svc = CreateService(TestFixtures.BuildSolution());
        var file = TestFixtures.BuildCoreProject().Files.First();

        var view = svc.GetFileMetadata(file.FilePath);

        Assert.Equal(file.FilePath, view.FilePath);
        Assert.Equal(file.LinesOfCode, view.LinesOfCode);
        Assert.Equal(file.Kind, view.Kind);
    }

    [Fact]
    public void GetFileMetadata_RelativePath_ResolvesToAbsolute()
    {
        var svc = CreateService(TestFixtures.BuildSolution());
        // 相对 solution 根目录的路径
        var rel = Path.GetRelativePath(TestFixtures.SolutionDir,
            @"C:\Projects\MyApp\MyApp.Core\Services\UserService.cs");

        var view = svc.GetFileMetadata(rel);

        Assert.Equal("Source", view.Kind);
    }

    [Fact]
    public void GetFileMetadata_RelativePath_OutputRelativePathIsRelative()
    {
        var svc = CreateService(TestFixtures.BuildSolution());
        var file = TestFixtures.BuildCoreProject().Files.First();

        var view = svc.GetFileMetadata(file.FilePath);

        Assert.NotNull(view.RelativePath);
        Assert.False(Path.IsPathRooted(view.RelativePath),
            $"'{view.RelativePath}' should be relative");
    }

    [Fact]
    public void GetFileMetadata_NonIndexedFile_ThrowsFileNotFound()
    {
        var svc = CreateService(TestFixtures.BuildSolution());

        // 完全不存在于索引也不存在于磁盘的路径
        Assert.Throws<FileNotFoundException>(() =>
            svc.GetFileMetadata(@"C:\Totally\Missing\File.cs"));
    }

    // ── GetRecentlyModifiedFiles ─────────────────────────────────────────

    [Fact]
    public void GetRecentlyModifiedFiles_OrderedByDescendingDate()
    {
        var svc = CreateService(TestFixtures.BuildSolution());
        var files = svc.GetRecentlyModifiedFiles();

        var dates = files.Select(f => f.LastWriteTimeUtc).ToList();
        Assert.Equal(dates.OrderByDescending(d => d), dates);
    }

    [Fact]
    public void GetRecentlyModifiedFiles_SinceFilter_ExcludesOlderFiles()
    {
        var since = new DateTime(2025, 5, 31, 0, 0, 0, DateTimeKind.Utc);
        var svc = CreateService(TestFixtures.BuildSolution());
        var files = svc.GetRecentlyModifiedFiles(sinceUtc: since);

        Assert.All(files, f => Assert.True(f.LastWriteTimeUtc >= since));
    }

    [Fact]
    public void GetRecentlyModifiedFiles_FutureSinceFilter_ReturnsEmpty()
    {
        var future = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var svc = CreateService(TestFixtures.BuildSolution());
        var files = svc.GetRecentlyModifiedFiles(sinceUtc: future);

        Assert.Empty(files);
    }

    [Fact]
    public void GetRecentlyModifiedFiles_CountIsRespected()
    {
        // 5 个文件，只取 3
        var project = TestFixtures.BuildCoreProject();
        var svc = CreateService(TestFixtures.BuildSolution(project));

        var files = svc.GetRecentlyModifiedFiles(count: 3);

        Assert.True(files.Count <= 3);
    }

    [Fact]
    public void GetRecentlyModifiedFiles_CountClampedToMaximum()
    {
        var svc = CreateService(TestFixtures.BuildSolution());
        // 超出上限不应抛出，最多返回 200
        var files = svc.GetRecentlyModifiedFiles(count: 9999);

        Assert.True(files.Count <= 200);
    }

    // ── DetectConventions ────────────────────────────────────────────────

    [Fact]
    public void DetectConventions_ReturnsSolutionLevelConventions()
    {
        var svc = CreateService(TestFixtures.BuildSolution());
        var conv = svc.DetectConventions();

        Assert.True(conv.HasEditorConfig);
        Assert.True(conv.UsesNullable);
        Assert.Equal("xUnit", conv.TestFrameworkHint);
    }
}