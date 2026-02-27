using LLMClient.ContextEngineering;
using LLMClient.ContextEngineering.PromptGeneration;

namespace LLMClient.Test.Agent.Tools;

public class FileTreeFormatterTests
{
    private readonly FileTreeFormatter _formatter = new();

    // ── 基础输出格式 ─────────────────────────────────────────────────────

    [Fact]
    public void Format_EmptyPaths_OutputsHeaderAndEmptyCodeBlock()
    {
        var result = _formatter.Format("# Test", Array.Empty<string>());

        Assert.Contains("# Test", result);
        Assert.Contains("```", result);
    }

    [Fact]
    public void Format_WithHeader_HeaderAppearsBeforeCodeBlock()
    {
        var result = _formatter.Format("# My Header", new[] { "Foo.cs" });

        var headerIndex   = result.IndexOf("# My Header", StringComparison.Ordinal);
        var codeBlockIndex = result.IndexOf("```", StringComparison.Ordinal);
        Assert.True(headerIndex < codeBlockIndex, "Header should appear before the code block");
    }

    [Fact]
    public void Format_EmptyHeader_NoHeaderLineOutput()
    {
        var result = _formatter.Format(string.Empty, new[] { "Foo.cs" });

        Assert.DoesNotContain("#", result.Split("```")[0]); // 代码块外无 # 标题
    }

    [Fact]
    public void Format_AlwaysWrapsInCodeBlock()
    {
        var result = _formatter.Format("H", new[] { "A.cs" });

        var count = result.Split("```").Length - 1;
        Assert.Equal(2, count); // 开头和结尾各一个 ```
    }

    // ── 树结构正确性 ─────────────────────────────────────────────────────

    [Fact]
    public void Format_SingleFile_ShowsFilename()
    {
        var result = _formatter.Format("H", new[] { "UserService.cs" });

        Assert.Contains("UserService.cs", result);
    }

    [Fact]
    public void Format_NestedPath_ShowsDirectoryAndFile()
    {
        var result = _formatter.Format("H", new[] { @"Services\UserService.cs" });

        Assert.Contains("Services", result);
        Assert.Contains("UserService.cs", result);
    }

    [Fact]
    public void Format_MultipleFilesInSameFolder_BothVisible()
    {
        var paths = new[] { @"Services\A.cs", @"Services\B.cs" };

        var result = _formatter.Format("H", paths);

        Assert.Contains("A.cs", result);
        Assert.Contains("B.cs", result);
    }

    [Fact]
    public void Format_DirectoriesAppearBeforeFiles()
    {
        // Services 目录和 Program.cs 同级
        var paths = new[] { @"Services\UserService.cs", "Program.cs" };

        var result = _formatter.Format("H", paths);

        var servicesIndex = result.IndexOf("Services", StringComparison.Ordinal);
        var programIndex  = result.IndexOf("Program.cs", StringComparison.Ordinal);
        Assert.True(servicesIndex < programIndex, "Directories should appear before sibling files");
    }

    [Fact]
    public void Format_DuplicatePaths_DeduplicatedInOutput()
    {
        var paths = new[] { "A.cs", "A.cs", "A.cs" };

        var result = _formatter.Format("H", paths);

        // "A.cs" 应只出现一次（在树节点中）
        var occurrences = CountOccurrences(result, "A.cs");
        Assert.Equal(1, occurrences);
    }

    // ── 深度限制 ─────────────────────────────────────────────────────────

    [Fact]
    public void Format_MaxDepth1_CollapsesNestedContent()
    {
        var paths = new[] { @"A\B\C\Deep.cs" };

        var result = _formatter.Format("H", paths, maxDepth: 1);

        // B 及以下被折叠
        Assert.DoesNotContain("Deep.cs", result);
        Assert.Contains("depth limit", result);
    }

    [Fact]
    public void Format_MaxDepth4_ShowsUpToFourLevels()
    {
        var paths = new[]
        {
            @"L1\L2\L3\L4\File.cs",
            @"L1\L2\L3\L4\L5\TooDeep.cs"
        };

        var result = _formatter.Format("H", paths, maxDepth: 4);

        Assert.Contains("File.cs", result);
        Assert.DoesNotContain("TooDeep.cs", result);
    }

    // ── 排除模式 ─────────────────────────────────────────────────────────

    [Fact]
    public void Format_ExcludePattern_RemovesMatchingPaths()
    {
        var paths = new[] { @"Services\UserService.cs", @"obj\Debug\Build.cs" };

        var result = _formatter.Format("H", paths, excludePatterns: new[] { "obj" });

        Assert.Contains("UserService.cs", result);
        Assert.DoesNotContain("Build.cs", result);
    }

    [Fact]
    public void Format_ExcludePattern_CaseInsensitive()
    {
        var paths = new[] { @"Generated\AutoCode.cs", @"Services\Real.cs" };

        var result = _formatter.Format("H", paths, excludePatterns: new[] { "generated" });

        Assert.DoesNotContain("AutoCode.cs", result);
        Assert.Contains("Real.cs", result);
    }

    [Fact]
    public void Format_NoExcludePatterns_AllPathsIncluded()
    {
        var paths = new[] { @"obj\Debug.cs", @"bin\Release.cs", "Program.cs" };

        var result = _formatter.Format("H", paths);

        // 未传 exclude 时全部显示
        Assert.Contains("Debug.cs", result);
        Assert.Contains("Release.cs", result);
        Assert.Contains("Program.cs", result);
    }

    // ── 条目截断 ─────────────────────────────────────────────────────────

    [Fact]
    public void Format_ExceedsMaxFilesPerFolder_ShowsTruncationMessage()
    {
        var paths = Enumerable.Range(1, 35).Select(i => $"File{i:D2}.cs");

        var result = _formatter.Format("H", paths, maxFilesPerFolder: 30);

        Assert.Contains("more items", result);
    }

    [Fact]
    public void Format_BelowMaxFilesPerFolder_NoTruncationMessage()
    {
        var paths = Enumerable.Range(1, 10).Select(i => $"File{i}.cs");

        var result = _formatter.Format("H", paths, maxFilesPerFolder: 30);

        Assert.DoesNotContain("more items", result);
    }

    // ── ProjectInfo 重载 ─────────────────────────────────────────────────

    [Fact]
    public void Format_ProjectInfo_UsesFilesIndex()
    {
        var project = TestFixtures.BuildCoreProject();

        var result = _formatter.Format(project);

        Assert.Contains("UserService.cs", result);
        Assert.Contains("MyApp.Core", result);
    }

    [Fact]
    public void Format_ProjectInfo_WhenFilesEmpty_FallsBackToTypes()
    {
        var project = TestFixtures.BuildCoreProject();
        project.Files.Clear(); // 强制触发 fallback

        var result = _formatter.Format(project);

        // fallback 从 Types.RelativePath 读取
        Assert.Contains("UserService.cs", result);
    }

    // ── SolutionInfo 重载 ────────────────────────────────────────────────

    [Fact]
    public void Format_SolutionInfo_ShowsAllProjects()
    {
        var solution = TestFixtures.BuildSolution(
            TestFixtures.BuildCoreProject(),
            TestFixtures.BuildApiProject());

        var result = _formatter.Format(solution);

        Assert.Contains("MyApp.Core", result);
        Assert.Contains("MyApp.Api", result);
    }

    [Fact]
    public void Format_SolutionInfo_ShowsFrameworksInHeader()
    {
        var solution = TestFixtures.BuildSolution();

        var result = _formatter.Format(solution);

        Assert.Contains("net9.0", result);
    }

    // ── 工具方法 ─────────────────────────────────────────────────────────

    private static int CountOccurrences(string source, string target)
    {
        var count = 0;
        var idx = 0;
        while ((idx = source.IndexOf(target, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += target.Length;
        }
        return count;
    }
}