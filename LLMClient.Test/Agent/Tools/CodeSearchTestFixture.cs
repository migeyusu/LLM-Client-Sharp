// File: LLMClient.Test/Agent/Tools/CodeSearchTestFixture.cs

using AutoMapper;
using LLMClient.ContextEngineering.Analysis;
using LLMClient.ContextEngineering.Tools;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LLMClient.Test.Agent.Tools;

/// <summary>
/// 代码搜索测试的共享 Fixture，提供模拟的解决方案数据和依赖服务
/// </summary>
public sealed class CodeSearchTestFixture : IDisposable
{
    internal SolutionContext Context { get; }
    public Mock<IEmbeddingService> MockEmbeddingService { get; }
    internal CodeSearchService SearchService { get; }
    public string TestSolutionDir { get; }
    public string TestSolutionPath { get; }
    
    // ✅ 暴露真实的 IndexService，用于手动填充测试数据
    public SymbolIndexService IndexService { get; }

    // 测试用临时文件
    private readonly List<string> _tempFiles = new();

    public CodeSearchTestFixture()
    {
        // 创建测试目录结构
        TestSolutionDir = Path.Combine(Path.GetTempPath(), $"CodeSearchTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(TestSolutionDir);
        TestSolutionPath = Path.Combine(TestSolutionDir, "TestSolution.sln");

        // ✅ 创建真实的 IndexService
        IndexService = new SymbolIndexService();

        // 创建模拟的 SolutionInfo
        var solutionInfo = CreateMockSolutionInfo();

        // 创建实际的测试文件（供文本搜索使用）
        CreateTestFiles();

        // ✅ 配置 Mock Analyzer 返回真实的 IndexService
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<RoslynMappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var mockAnalyzer = new Mock<RoslynProjectAnalyzer>(null, mapper);
        mockAnalyzer.SetupGet(a => a.IndexService).Returns(IndexService);

        Context = new SolutionContext(mockAnalyzer.Object);
        Context.SetForTesting(solutionInfo);

        // ✅ 手动填充索引（从 solutionInfo 提取符号）
        PopulateIndexFromSolutionInfo(solutionInfo);

        // 配置 Mock EmbeddingService
        MockEmbeddingService = new Mock<IEmbeddingService>();
        SetupDefaultEmbeddingBehavior();

        // 创建服务实例
        SearchService = new CodeSearchService(Context, MockEmbeddingService.Object);
    }

    private SolutionInfo CreateMockSolutionInfo()
    {
        var projectPath = Path.Combine(TestSolutionDir, "TestProject", "TestProject.csproj");
        var userServicePath = Path.Combine(TestSolutionDir, "TestProject", "Services", "UserService.cs");
        var authServicePath = Path.Combine(TestSolutionDir, "TestProject", "Services", "AuthService.cs");
        var configPath = Path.Combine(TestSolutionDir, "TestProject", "Config.json");

        var userServiceMember = new MemberInfo
        {
            UniqueId = "M:TestProject.Services.UserService.GetUserAsync(System.Int32)",
            Name = "GetUserAsync",
            Signature = "public async Task<User> GetUserAsync(int userId)",
            Kind = "Method",
            Accessibility = "Public",
            Attributes = new List<string>(),
            Summary = "Gets user by ID from database",
            Locations = new List<CodeLocation>
            {
                new()
                {
                    FilePath = userServicePath,
                    Location = new LinePositionSpan(
                        new LinePosition(10, 1),
                        new LinePosition(15, 1)
                    )
                }
            },
            IsAsync = true,
            ReturnType = "Task<User>",
            Parameters = new List<ParameterInfo>
            {
                new() { Name = "userId", Type = "int", HasDefaultValue = false, DefaultValue = null }
            }
        };

        var obsoleteMember = new MemberInfo
        {
            UniqueId = "M:TestProject.Services.UserService.LegacyMethod",
            Name = "LegacyMethod",
            Signature = "public void LegacyMethod()",
            Kind = "Method",
            Accessibility = "Public",
            Attributes = new List<string> { "Obsolete" }, // ✅ 确保有 Obsolete 特性
            Summary = "Deprecated method",
            Locations = new List<CodeLocation>
            {
                new()
                {
                    FilePath = userServicePath,
                    Location = new LinePositionSpan(
                        new LinePosition(20, 1),
                        new LinePosition(23, 1)
                    )
                }
            }
        };

        var userServiceType = new TypeInfo
        {
            UniqueId = "T:TestProject.Services.UserService",
            Name = "UserService",
            Signature = "public class UserService : IUserService",
            Kind = "Class",
            Accessibility = "Public",
            Attributes = new List<string>(),
            Summary = "Service for user management",
            Locations = new List<CodeLocation>
            {
                new()
                {
                    FilePath = userServicePath,
                    Location = new LinePositionSpan(
                        new LinePosition(5, 1),
                        new LinePosition(30, 1)
                    )
                }
            },
            ImplementedInterfaces = new List<string> { "IUserService" },
            Members = { userServiceMember, obsoleteMember }
        };

        var authServiceType = new TypeInfo
        {
            UniqueId = "T:TestProject.Services.AuthService",
            Name = "AuthService",
            Signature = "public class AuthService",
            Kind = "Class",
            Accessibility = "Public",
            Attributes = new List<string>(),
            Summary = "Service for authentication",
            Locations = new List<CodeLocation>
            {
                new()
                {
                    FilePath = authServicePath,
                    Location = new LinePositionSpan(
                        new LinePosition(5, 1),
                        new LinePosition(25, 1)
                    )
                }
            }
        };

        var testProject = new ProjectInfo
        {
            Name = "TestProject",
            ProjectFilePath = projectPath,
            RelativeRootDir = "TestProject",
            FullRootDir = Path.Combine(TestSolutionDir, "TestProject"),
            OutputType = "Library",
            Language = "C#",
            LanguageVersion = "latest",
            TargetFrameworks = new List<string> { "net8.0" },
            Namespaces = new List<NamespaceInfo>
            {
                new()
                {
                    Name = "TestProject.Services",
                    FilePath = userServicePath,
                    Types = { userServiceType, authServiceType }
                }
            },
            Files = new List<FileEntryInfo>
            {
                new()
                {
                    FilePath = userServicePath,
                    RelativePath = "Services/UserService.cs",
                    ProjectFilePath = projectPath,
                    Extension = ".cs",
                    Kind = "Source",
                    SizeBytes = 1024,
                    LinesOfCode = 30,
                    LastWriteTimeUtc = DateTime.UtcNow.AddDays(-2)
                },
                new()
                {
                    FilePath = authServicePath,
                    RelativePath = "Services/AuthService.cs",
                    ProjectFilePath = projectPath,
                    Extension = ".cs",
                    Kind = "Source",
                    SizeBytes = 800,
                    LinesOfCode = 25,
                    LastWriteTimeUtc = DateTime.UtcNow.AddDays(-1)
                },
                new()
                {
                    FilePath = configPath,
                    RelativePath = "Config.json",
                    ProjectFilePath = projectPath,
                    Extension = ".json",
                    Kind = "Config",
                    SizeBytes = 200,
                    LinesOfCode = 10,
                    LastWriteTimeUtc = DateTime.UtcNow
                }
            },
            Statistics = new ProjectStatistics
            {
                FilesCount = 3,
                TypesCount = 2,
                MethodsCount = 2,
                LinesOfCode = 65
            }
        };

        return new SolutionInfo
        {
            SolutionName = "TestSolution",
            SolutionPath = TestSolutionPath,
            Projects = new List<ProjectInfo> { testProject },
            GeneratedAt = DateTime.UtcNow
        };
    }

    // ✅ 新增：从 SolutionInfo 手动填充索引
    private void PopulateIndexFromSolutionInfo(SolutionInfo solutionInfo)
    {
        foreach (var project in solutionInfo.Projects)
        {
            foreach (var ns in project.Namespaces)
            {
                foreach (var type in ns.Types)
                {
                    // 添加类型
                    IndexService.AddSymbol(type);

                    // 添加成员
                    foreach (var member in type.Members)
                    {
                        IndexService.AddSymbol(member);
                    }
                }
            }
        }
    }

    private void CreateTestFiles()
    {
        var projectDir = Path.Combine(TestSolutionDir, "TestProject");
        var servicesDir = Path.Combine(projectDir, "Services");
        Directory.CreateDirectory(servicesDir);

        // UserService.cs
        var userServicePath = Path.Combine(servicesDir, "UserService.cs");
        var userServiceContent = @"using System;
using System.Threading.Tasks;

namespace TestProject.Services
{
    public class UserService : IUserService
    {
        private readonly HttpClient _httpClient;

        public async Task<User> GetUserAsync(int userId)
        {
            var url = $""/api/users/{userId}"";
            var response = await _httpClient.GetAsync(url);
            return await response.Content.ReadAsAsync<User>();
        }

        // TODO: Refactor this method
        [Obsolete(""Use GetUserAsync instead"")]
        public void LegacyMethod()
        {
            // Old implementation
            Console.WriteLine(""Legacy code"");
        }

        public void ProcessUser(User user)
        {
            // Authentication logic here
            ValidateUser(user);
        }
    }
}";
        File.WriteAllText(userServicePath, userServiceContent);
        _tempFiles.Add(userServicePath);

        // AuthService.cs
        var authServicePath = Path.Combine(servicesDir, "AuthService.cs");
        var authServiceContent = @"using System;
using System.Threading.Tasks;

namespace TestProject.Services
{
    public class AuthService
    {
        private readonly HttpClient _httpClient;

        public async Task<string> AuthenticateAsync(string username, string password)
        {
            // Authentication logic
            var response = await _httpClient.PostAsync(""/api/auth"", null);
            return await response.Content.ReadAsStringAsync();
        }

        public bool ValidateToken(string token)
        {
            // JWT token validation
            return !string.IsNullOrEmpty(token);
        }
    }
}";
        File.WriteAllText(authServicePath, authServiceContent);
        _tempFiles.Add(authServicePath);

        // Config.json
        var configPath = Path.Combine(projectDir, "Config.json");
        var configContent = @"{
  ""AppSettings"": {
    ""ApiUrl"": ""https://api.example.com"",
    ""Timeout"": 30
  }
}";
        File.WriteAllText(configPath, configContent);
        _tempFiles.Add(configPath);
    }

    private void SetupDefaultEmbeddingBehavior()
    {
        MockEmbeddingService
            .Setup(s => s.SearchByEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string query, int topK, CancellationToken _) =>
            {
                var results = new List<(string filePath, int startLine, int endLine, string snippet, double score)>();

                if (query.Contains("authentication", StringComparison.OrdinalIgnoreCase))
                {
                    var authServicePath = Path.Combine(TestSolutionDir, "TestProject", "Services", "AuthService.cs");
                    results.Add((
                        authServicePath,
                        10,
                        15,
                        "public async Task<string> AuthenticateAsync(string username, string password)\n{\n    // Authentication logic\n}",
                        0.95
                    ));
                }

                if (query.Contains("http", StringComparison.OrdinalIgnoreCase) ||
                    query.Contains("client", StringComparison.OrdinalIgnoreCase))
                {
                    var userServicePath = Path.Combine(TestSolutionDir, "TestProject", "Services", "UserService.cs");
                    results.Add((
                        userServicePath,
                        10,
                        14,
                        "var response = await _httpClient.GetAsync(url);",
                        0.88
                    ));
                }

                return results.Take(topK).ToList();
            });
    }

    public void Dispose()
    {
        // 清理临时文件
        try
        {
            if (Directory.Exists(TestSolutionDir))
            {
                Directory.Delete(TestSolutionDir, recursive: true);
            }
        }
        catch
        {
            // 忽略清理错误
        }
    }
}