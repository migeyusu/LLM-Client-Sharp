using AutoMapper;
using LLMClient.ContextEngineering.Analysis;
using LLMClient.ContextEngineering.Tools;
using Microsoft.Extensions.Logging;

namespace LLMClient.Test.Agent.Tools;

[Collection("CodeReading collection")]
public class CodeReadingServiceTests
{
    private static CodeReadingService CreateService(SolutionInfo solution)
    {
        var solutionContext = SolutionContextTestFactory.CreateLoaded(solution);

        var config = new MapperConfiguration(cfg => cfg.AddProfile<RoslynMappingProfile>(),
            LoggerFactory.Create(builder => builder.AddDebug()));
        var mapper = config.CreateMapper();

        return new CodeReadingService(solutionContext, mapper);
    }

    [Fact]
    public void GetFileOutline_MapsTypeAndMembersViaAutoMapper_AndPreservesFileSpecificLines()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CodeReadingServiceTests", Guid.NewGuid().ToString("N"));
        var projectRoot = Path.Combine(tempDir, "MyApp.Core");
        var userServicePath = Path.Combine(projectRoot, "Services", "UserService.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(userServicePath)!);
        File.WriteAllText(userServicePath,
            string.Join(Environment.NewLine, Enumerable.Range(1, 50).Select(i => $"// line {i}")));

        var memberB = new MemberInfo
        {
            UniqueId = "M:MyApp.Core.Services.UserService.Zeta",
            Name = "Zeta",
            Kind = "Method",
            Signature = "public void Zeta()",
            Accessibility = "Public",
            Summary = "last",
            ReturnType = "void",
            Parameters = [],
            Locations = [SymbolSemanticFixtures.Loc(userServicePath, 25, 28)]
        };

        var memberA = new MemberInfo
        {
            UniqueId = "M:MyApp.Core.Services.UserService.Alpha",
            Name = "Alpha",
            Kind = "Method",
            Signature = "private void Alpha()",
            Accessibility = "Private",
            Summary = "first",
            ReturnType = "void",
            Parameters = [],
            Locations = [SymbolSemanticFixtures.Loc(userServicePath, 10, 12)]
        };

        var type = new TypeInfo
        {
            UniqueId = "T:MyApp.Core.Services.UserService",
            Name = "UserService",
            Kind = "Class",
            Signature = "public class UserService",
            Accessibility = "Public",
            Summary = "Handles users",
            Locations = [SymbolSemanticFixtures.Loc(userServicePath, 5, 40)]
        };
        type.Members.Add(memberB);
        type.Members.Add(memberA);

        var project = TestFixtures.BuildCoreProject(files:
        [
            TestFixtures.BuildFile(projectRoot, @"Services\UserService.cs", "Source", 50,
                new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc))
        ]);
        project.ProjectFilePath = Path.Combine(projectRoot, "MyApp.Core.csproj");
        project.FullRootDir = projectRoot;
        project.Namespaces.Clear();
        project.Namespaces.Add(new NamespaceInfo
        {
            Name = "MyApp.Core.Services",
            FilePath = project.ProjectFilePath,
            Types = [type]
        });

        var solution = TestFixtures.BuildSolution(project);
        solution.SolutionPath = Path.Combine(tempDir, "MyApp.sln");

        var svc = CreateService(solution);

        var outline = svc.GetFileOutline(userServicePath);

        var ns = Assert.Single(outline.Namespaces);
        var typeView = Assert.Single(ns.Types);
        Assert.Equal(type.SymbolId, typeView.SymbolId);
        Assert.Equal(type.Signature, typeView.Signature);
        Assert.Equal(type.Summary, typeView.Summary);
        Assert.Equal(5, typeView.StartLine);
        Assert.Equal(40, typeView.EndLine);

        Assert.Collection(typeView.Members,
            firstMember =>
            {
                Assert.Equal(memberA.SymbolId, firstMember.SymbolId);
                Assert.Equal(memberA.Accessibility, firstMember.Accessibility);
                Assert.Equal(memberA.Summary, firstMember.Summary);
                Assert.Equal(10, firstMember.StartLine);
            },
            secondMember =>
            {
                Assert.Equal(memberB.SymbolId, secondMember.SymbolId);
                Assert.Equal(memberB.Signature, secondMember.Signature);
                Assert.Equal(25, secondMember.StartLine);
            });
    }

    [Fact]
    public void ListFiles_MapsFileMetadataViaAutoMapper_AndCalculatesRelativePath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CodeReadingServiceTests_ListFiles",
            Guid.NewGuid().ToString("N"));
        var projectRoot = Path.Combine(tempDir, "MyApp.Core");
        var serviceFile = Path.Combine(projectRoot, "Services", "UserService.cs");

        var project = TestFixtures.BuildCoreProject();
        project.FullRootDir = projectRoot;
        project.ProjectFilePath = Path.Combine(projectRoot, "MyApp.Core.csproj");

        var entry = new FileEntryInfo
        {
            FilePath = serviceFile,
            RelativePath = @"Services\UserService.cs",
            ProjectFilePath = project.ProjectFilePath,
            Extension = ".cs",
            SizeBytes = 1234,
            LinesOfCode = 100,
            LastWriteTimeUtc = new DateTime(2025, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            Kind = "Source"
        };
        project.Files.Clear();
        project.Files.Add(entry);

        var solution = TestFixtures.BuildSolution(project);
        solution.SolutionPath = Path.Combine(tempDir, "MyApp.sln");

        var svc = CreateService(solution);

        // Act
        var result = svc.ListFiles(path: ".", recursive: true);

        // Assert
        var file = Assert.Single(result.Files);
        Assert.Equal(entry.FilePath, file.FilePath);
        // path is relative to solution dir, which is tempDir
        // serviceFile is tempDir/MyApp.Core/Services/UserService.cs
        // correct relative path should be MyApp.Core/Services/UserService.cs
        var expectedRelativePath = Path.Combine("MyApp.Core", "Services", "UserService.cs");
        Assert.Equal(expectedRelativePath, file.RelativePath);

        Assert.Equal(entry.Extension, file.Extension);
        Assert.Equal(entry.Kind, file.Kind);
        Assert.Equal(entry.SizeBytes, file.SizeBytes);
        Assert.Equal(entry.LinesOfCode, file.LinesOfCode);
        Assert.Equal(entry.LastWriteTimeUtc, file.LastWriteTimeUtc);
    }

    private readonly CodeReadingFixture _fixture;
    private readonly ICodeReadingService _service;

    public CodeReadingServiceTests(CodeReadingFixture fixture)
    {
        _fixture = fixture;
        _service = fixture.Service;
    }

    [Fact]
    public void ReadFile_ReturnsFullContent_WhenNoRangeOrTokenLimit()
    {
        var result = _service.ReadFile(_fixture.RelativeSourcePath);

        Assert.NotNull(result);
        Assert.Equal(_fixture.SourceFilePath, result.FilePath);
        Assert.False(result.Truncated);
        Assert.True(result.TotalLines >= 1);
        Assert.Equal(1, result.StartLine);
        Assert.Equal(result.TotalLines, result.EndLine);
        Assert.Contains("namespace TestNamespace", result.Content);
        Assert.Contains("public class TestClass", result.Content);
    }

    [Fact]
    public void ReadFile_RespectsLineRange()
    {
        var start = _fixture.AddMethodBodyStartLine;
        var end = _fixture.AddMethodBodyEndLine;

        var result = _service.ReadFile( _fixture.RelativeSourcePath,
            startLine: start,
            endLine: end);

        Assert.Equal(start, result.StartLine);
        Assert.Equal(end, result.EndLine);
        Assert.Contains("public int Add", result.Content);
        Assert.Contains("return a + b;", result.Content);
        Assert.DoesNotContain("namespace TestNamespace", result.Content);
    }

    [Fact]
    public void ReadFile_Truncates_WhenTokenLimitExceeded()
    {
        var full = _service.ReadFile(_fixture.RelativeSourcePath);

        // 将预算设为完全读取的一半，保证触发截断逻辑
        var smallBudget = full.TokenEstimate / 2;
        if (smallBudget < 10) smallBudget = 10;

        var truncated = _service.ReadFile(
            _fixture.RelativeSourcePath,
            startLine: 1,
            endLine: full.TotalLines,
            maxTokens: smallBudget);

        Assert.True(truncated.Truncated);
        Assert.True(truncated.TokenEstimate <= smallBudget);
        Assert.True(truncated.EndLine < full.EndLine);
    }

    [Fact]
    public async Task ReadSymbolBodyAsync_ReturnsBodyWithContext_FromIndex()
    {
        var contextLines = 1;

        var result = await _service.ReadSymbolBodyAsync(
            _fixture.AddMethodSymbolId,
            contextLines: contextLines);

        Assert.Equal(_fixture.AddMethodSymbolId, result.SymbolId);
        Assert.Equal("Add", result.Name);
        Assert.Equal(_fixture.SourceFilePath, result.FilePath);
        // 测试环境通过 SetForTesting，SolutionContext.IsLoaded == false，因此 Source 应为 "Index"
        Assert.Equal("Index", result.Source);

        Assert.Equal(_fixture.AddMethodBodyStartLine, result.BodyStartLine);
        Assert.Equal(_fixture.AddMethodBodyEndLine, result.BodyEndLine);
        Assert.True(result.ContentStartLine <= result.BodyStartLine - contextLines);
        Assert.True(result.ContentEndLine >= result.BodyEndLine + contextLines);

        Assert.Contains("public int Add", result.Content);
        Assert.Contains("return a + b;", result.Content);
    }

    [Fact]
    public void GetFileOutline_ReturnsNamespacesTypesAndMembers()
    {
        var outline = _service.GetFileOutline(_fixture.RelativeSourcePath);

        Assert.Equal(_fixture.SourceFilePath, outline.FilePath);
        Assert.True(outline.TotalLines >= 1);
        Assert.Single(outline.Namespaces);

        var ns = outline.Namespaces.Single();
        Assert.Equal("TestNamespace", ns.Name);
        Assert.Single(ns.Types);

        var type = ns.Types.Single();
        Assert.Equal("TestClass", type.Name);
        Assert.Equal("Class", type.Kind);
        Assert.Single(type.Members);

        var member = type.Members.Single();
        Assert.Equal("Add", member.Name);
        Assert.Equal(_fixture.AddMethodSymbolId, member.SymbolId);
        Assert.True(member.StartLine >= _fixture.AddMethodBodyStartLine);
    }

    [Fact]
    public void ListFiles_ReturnsFilteredFilesUnderRoot()
    {
        var list = _service.ListFiles(
            path: ".",
            filter: ".cs",
            recursive: true,
            maxCount: 10);

        Assert.NotNull(list);
        Assert.True(list.TotalCount >= 1);
        Assert.False(list.Truncated);
        Assert.Contains(list.Files, f => f.FilePath == _fixture.SourceFilePath);

        // 非递归：只查项目目录（不含子目录）
        var projectDir = Path.GetDirectoryName(_fixture.SourceFilePath)!;
        var projectRelative = Path.GetRelativePath(_fixture.RootDir, projectDir);

        var list2 = _service.ListFiles(
            path: projectRelative,
            filter: "TestClass",
            recursive: false,
            maxCount: 10);

        Assert.Contains(list2.Files, f => f.FilePath == _fixture.SourceFilePath);
    }
}