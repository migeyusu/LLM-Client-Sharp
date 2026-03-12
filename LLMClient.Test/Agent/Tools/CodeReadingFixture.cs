// CodeReadingFixture.cs

using AutoMapper;
using LLMClient.ContextEngineering.Analysis;
using LLMClient.ContextEngineering.Tools;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging.Abstractions;

namespace LLMClient.Test.Agent.Tools;

[CollectionDefinition("CodeReading collection")]
public class CodeReadingCollection : ICollectionFixture<CodeReadingFixture>
{
}

public sealed class CodeReadingFixture : IDisposable
{
    public string RootDir { get; }
    public string SolutionPath { get; }
    public string ProjectFilePath { get; }
    public string SourceFilePath { get; }
    public string RelativeSourcePath { get; }

    internal SolutionContext Context { get; }
    public ICodeReadingService Service { get; }
    public CodeReadingPlugin Plugin { get; }

    public string AddMethodSymbolId { get; }
    public int AddMethodBodyStartLine { get; }
    public int AddMethodBodyEndLine { get; }

    public CodeReadingFixture()
    {
        RootDir = Path.Combine(Path.GetTempPath(), "CodeReadingTests_" + Guid.NewGuid());
        Directory.CreateDirectory(RootDir);

        SolutionPath = Path.Combine(RootDir, "TestSolution.sln");
        var projectDir = Path.Combine(RootDir, "TestProject");
        Directory.CreateDirectory(projectDir);
        ProjectFilePath = Path.Combine(projectDir, "TestProject.csproj");

        SourceFilePath = Path.Combine(projectDir, "TestClass.cs");
        File.WriteAllText(SourceFilePath, TestSourceCode);
        RelativeSourcePath = Path.GetRelativePath(RootDir, SourceFilePath);

        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<RoslynMappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var analyzer = new RoslynProjectAnalyzer(logger: null, mapper: mapper);
        Context = new SolutionContext(analyzer);

        // 读取文件行，用于计算行号
        var lines = File.ReadAllLines(SourceFilePath);

        // 构造方法符号（含 CodeLocation）
        var (member, symbolId, startLine, endLine) = BuildAddMethodSymbol(SourceFilePath, lines);
        AddMethodSymbolId = symbolId;
        AddMethodBodyStartLine = startLine;
        AddMethodBodyEndLine = endLine;

        // 构造 TypeInfo / NamespaceInfo
        var typeInfo = BuildTypeInfo(SourceFilePath, lines, member);
        var nsInfo = new NamespaceInfo
        {
            Name = "TestNamespace",
            FilePath = SourceFilePath
        };
        nsInfo.Types.Add(typeInfo);

        // 构造 FileEntryInfo
        var fileInfo = new FileInfo(SourceFilePath);
        var fileEntry = new FileEntryInfo
        {
            FilePath = SourceFilePath,
            RelativePath = Path.GetFileName(SourceFilePath),
            ProjectFilePath = ProjectFilePath,
            Extension = ".cs",
            SizeBytes = fileInfo.Length,
            LinesOfCode = lines.Length,
            LastWriteTimeUtc = fileInfo.LastWriteTimeUtc,
            Kind = "Source"
        };

        // 构造 ProjectInfo / SolutionInfo
        var project = new ProjectInfo
        {
            Name = "TestProject",
            ProjectFilePath = ProjectFilePath,
            RelativeRootDir = ".",
            FullRootDir = projectDir,
            OutputType = "Library",
            Language = "C#",
            LanguageVersion = "latest",
            GeneratedAt = DateTime.UtcNow
        };
        project.Files.Add(fileEntry);
        project.Namespaces.Add(nsInfo);

        var solution = new SolutionInfo
        {
            SolutionName = "TestSolution",
            SolutionPath = SolutionPath,
            GeneratedAt = DateTime.UtcNow,
            Conventions = new ConventionInfo()
        };
        solution.Projects = new List<ProjectInfo> { project };

        // 把方法符号加入索引，供 read_symbol_body 使用
        analyzer.IndexService.AddSymbol(member);

        // 设置到 SolutionContext（走 SetForTesting 覆盖）
        Context.SetForTesting(solution);

        Service = new CodeReadingService(Context, mapper, NullLogger<CodeReadingService>.Instance);
        Plugin = new CodeReadingPlugin(Service);
    }

    private static (MemberInfo member, string symbolId, int startLine, int endLine)
        BuildAddMethodSymbol(string filePath, string[] lines)
    {
        // 定位 "public int Add" 行（0-based 索引）
        var methodStartIdx = Array.FindIndex(lines, l => l.Contains("public int Add"));
        if (methodStartIdx < 0)
            throw new InvalidOperationException("Test source does not contain 'public int Add'.");

        // 从该行往后找到第一个 '}'，作为方法结束行
        var methodEndIdx = Array.FindIndex(lines, methodStartIdx, l => l.Contains("}"));
        if (methodEndIdx < 0)
            throw new InvalidOperationException("Test source does not contain method closing brace.");

        var startLine = methodStartIdx + 1; // 转为 1-based
        var endLine = methodEndIdx + 1;

        var codeLocation = new CodeLocation
        {
            FilePath = filePath,
            Location = new LinePositionSpan(
                new LinePosition(startLine, 1),
                new LinePosition(endLine, 1))
        };

        var symbolId = "M:TestNamespace.TestClass.Add(System.Int32,System.Int32)";
        var member = new MemberInfo
        {
            UniqueId = symbolId,
            Name = "Add",
            Signature = "public int Add(int a, int b)",
            Kind = "Method",
            Accessibility = "Public",
            Attributes = new List<string>(),
            Summary = "Adds two integers.",
            Locations = new List<CodeLocation> { codeLocation },
            IsStatic = false,
            IsAsync = false,
            IsVirtual = false,
            IsOverride = false,
            ReturnType = "int",
            Parameters = new List<ParameterInfo>
            {
                new()
                {
                    Name = "a",
                    Type = "int",
                    HasDefaultValue = false,
                    DefaultValue = null
                },
                new()
                {
                    Name = "b",
                    Type = "int",
                    HasDefaultValue = false,
                    DefaultValue = null
                }
            }
        };

        return (member, symbolId, startLine, endLine);
    }

    private static TypeInfo BuildTypeInfo(string filePath, string[] lines, MemberInfo member)
    {
        var typeStartIdx = Array.FindIndex(lines, l => l.Contains("public class TestClass"));
        if (typeStartIdx < 0)
            throw new InvalidOperationException("Test source does not contain 'public class TestClass'.");

        // 简单处理：最后一个仅含 '}' 的行视为类型结束（足够覆盖测试场景）
        var typeEndIdx = Array.FindLastIndex(lines, l => l.Trim() == "}");
        if (typeEndIdx < 0) typeEndIdx = lines.Length - 1;

        var typeLocation = new CodeLocation
        {
            FilePath = filePath,
            Location = new LinePositionSpan(
                new LinePosition(typeStartIdx + 1, 1),
                new LinePosition(typeEndIdx + 1, 1))
        };

        var type = new TypeInfo
        {
            UniqueId = "T:TestNamespace.TestClass",
            Name = "TestClass",
            Signature = "public class TestClass",
            Kind = "Class",
            Accessibility = "Public",
            Attributes = new List<string>(),
            Summary = "Test class for CodeReadingService.",
            Locations = new List<CodeLocation> { typeLocation },
            IsPartial = false,
            IsAbstract = false,
            IsSealed = false,
            ImplementedInterfaces = new List<string>()
        };
        type.Members.Add(member);

        return type;
    }

    private const string TestSourceCode = """
                                          namespace TestNamespace
                                          {
                                              public class TestClass
                                              {
                                                  public int Add(int a, int b)
                                                  {
                                                      return a + b;
                                                  }
                                              }
                                          }
                                          """;

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootDir))
            {
                Directory.Delete(RootDir, recursive: true);
            }
        }
        catch
        {
            // 清理失败忽略
        }
    }
}