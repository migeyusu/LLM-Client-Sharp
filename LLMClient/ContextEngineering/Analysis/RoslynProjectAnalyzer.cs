using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using AutoMapper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;

namespace LLMClient.ContextEngineering.Analysis;

public partial class RoslynProjectAnalyzer : IDisposable
{
    private readonly MSBuildWorkspace _workspace;
    private readonly AnalyzerConfig _config;
    private readonly ILogger? _logger;
    private readonly IMapper _mapper;
    private readonly ConcurrentDictionary<string, IList<DocumentAnalysisResult>> _docCache = new();

    public virtual SymbolIndexService IndexService { get; } = new();
    public Solution? CurrentRawSolution { get; private set; }

    public string? SolutionDir { get; private set; }

    public SolutionInfo? SolutionInfo { get; private set; }

    [MemberNotNullWhen(true, nameof(SolutionInfo), nameof(CurrentRawSolution), nameof(SolutionDir))]
    public bool IsLoaded => SolutionInfo != null;

    public RoslynProjectAnalyzer(ILogger<RoslynProjectAnalyzer>? logger, IMapper mapper, AnalyzerConfig? config = null)
    {
        _config = config ?? new AnalyzerConfig();
        _logger = logger;
        _mapper = mapper;
        _workspace = MSBuildWorkspace.Create();
        
        _workspace.RegisterWorkspaceFailedHandler(args =>
        {
            var e = args.Diagnostic;
            if (e.Kind == WorkspaceDiagnosticKind.Warning)
                _logger?.LogWarning($"MSBuild warning: {e.Message}");
            else
                _logger?.LogError($"MSBuild error: {e.Message}");
        });
    }


    public async Task LoadSolutionAsync(
        string solutionPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(solutionPath))
            throw new FileNotFoundException($"Solution file not found: {solutionPath}");
        var stopwatch = Stopwatch.StartNew();
        _logger?.LogInformation($"Starting analysis of {Path.GetFileName(solutionPath)}...");
        try
        {
            SolutionDir = Path.GetDirectoryName(solutionPath)
                          ?? throw new ArgumentException("Invalid solution path");
            var solution = await _workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken);
            var solutionInfo = new SolutionInfo
            {
                SolutionName = Path.GetFileNameWithoutExtension(solutionPath),
                SolutionPath = solutionPath,
                GeneratedAt = DateTime.UtcNow
            };
            await AnalysisSolutionAsync(solution, solutionInfo, cancellationToken);
            stopwatch.Stop();
            solutionInfo.GenerationTime = stopwatch.Elapsed;
            _logger?.LogDebug($"Analysis completed in {stopwatch.ElapsedMilliseconds}ms");
            CurrentRawSolution = solution;
            SolutionInfo = solutionInfo;
        }
        catch (Exception ex)
        {
            CurrentRawSolution = null;
            SolutionDir = null;
            SolutionInfo = null;
            _workspace.CloseSolution();
            _logger?.LogError($"Solution analysis failed: {ex}");
            throw;
        }
    }

    public async Task AnalysisCurrentSolutionAsync(CancellationToken cancellationToken = default)
    {
        if (!IsLoaded)
        {
            throw new InvalidOperationException("Solution is not loaded");
        }

        await AnalysisSolutionAsync(CurrentRawSolution, SolutionInfo, cancellationToken);
    }

    private async Task AnalysisSolutionAsync(Solution solution, SolutionInfo solutionInfo,
        CancellationToken cancellationToken = default)
    {
        // 过滤并并行分析项目
        var projects = solution.Projects
            .Where(ShouldIncludeProject)
            .ToList();
        _logger?.LogInformation($"Found {projects.Count} projects to analyze");
        var projectTasks = projects
            .Select(async p =>
            {
                try
                {
                    return await AnalyzeProjectAsync(p, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Failed to analyze project {p.Name}: {ex.Message}");
                    return null;
                }
            })
            .ToArray();

        var results = await Task.WhenAll(projectTasks);
        solutionInfo.Projects = results.Where(r => r != null).OfType<ProjectInfo>().ToList();
        // 计算统计信息
        CalculateSummaryStatistics(solutionInfo);
        solutionInfo.Conventions = MergeSolutionConventions(solutionInfo.Projects);
    }

    private static ConventionInfo MergeSolutionConventions(List<ProjectInfo> projects)
    {
        var c = new ConventionInfo();

        var anyEditorConfig = projects.Select(p => p.Conventions.EditorConfigPath)
            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
        if (anyEditorConfig != null)
        {
            c.HasEditorConfig = true;
            c.EditorConfigPath = anyEditorConfig;
        }

        c.UsesNullable = projects.Any(p => p.Conventions.UsesNullable);
        c.UsesImplicitUsings = projects.Any(p => p.Conventions.UsesImplicitUsings);
        c.TestFrameworkHint = projects
            .Select(p => p.Conventions.TestFrameworkHint)
            .FirstOrDefault(x => x != "Unknown") ?? "Unknown";

        return c;
    }

    private bool ShouldIncludeProject(Microsoft.CodeAnalysis.Project? project)
    {
        if (project == null || string.IsNullOrEmpty(project.FilePath))
            return false;

        var projectName = project.Name;
        var projectPath = project.FilePath;

        // 检查测试项目
        if (!_config.IncludeTestProjects && IsTestProject(project))
        {
            _logger?.LogDebug($"Excluding test project: {projectName}");
            return false;
        }

        // 检查示例项目
        if (!_config.IncludeSampleProjects &&
            (projectName.Contains("Sample", StringComparison.OrdinalIgnoreCase) ||
             projectName.Contains("Example", StringComparison.OrdinalIgnoreCase)))
        {
            _logger?.LogDebug($"Excluding sample project: {projectName}");
            return false;
        }

        // 检查排除模式
        foreach (var pattern in _config.ExcludedPatterns)
        {
            if (projectPath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogDebug($"Excluding project by pattern '{pattern}': {projectName}");
                return false;
            }
        }

        return true;
    }

    private static bool IsTestProject(Microsoft.CodeAnalysis.Project project)
    {
        var name = project.Name.ToLowerInvariant();
        return name.Contains("test") ||
               name.Contains("spec") ||
               name.EndsWith(".tests") ||
               name.EndsWith(".specs") ||
               project.MetadataReferences.Any(r => r.Display?.Contains("xunit") == true ||
                                                   r.Display?.Contains("nunit") == true ||
                                                   r.Display?.Contains("mstest") == true);
    }

    private async Task<ProjectInfo?> AnalyzeProjectAsync(
        Microsoft.CodeAnalysis.Project project,
        CancellationToken cancellationToken)
    {
        _logger?.LogInformation($"Analyzing project: {project.Name}");
        var stopwatch = Stopwatch.StartNew();
        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation == null) return null;
        var projectFilePath = project.FilePath;
        if (string.IsNullOrEmpty(projectFilePath))
        {
            return null;
        }

        var solutionDir = Path.GetDirectoryName(project.Solution.FilePath);
        var info = new ProjectInfo
        {
            Name = project.Name,
            ProjectFilePath = projectFilePath,
            RelativeRootDir = solutionDir == null ? "." : Path.GetRelativePath(solutionDir, projectFilePath),
            FullRootDir = Path.GetDirectoryName(projectFilePath) ?? string.Empty,
            OutputType = compilation.Options.OutputKind.ToString(),
            GeneratedAt = DateTime.UtcNow
        };

        // 提取目标框架
        ExtractTargetFrameworks(project, info);

        // 提取包引用
        ExtractPackageReferences(project, info);

        // 提取项目引用
        ExtractProjectReferences(project, info);

        // 并行分析文档
        var documents = project.Documents
            .Where(d => d.SupportsSyntaxTree && ShouldIncludeFile(d))
            .ToList();
        
        _logger?.LogDebug($"Analyzing {documents.Count} documents in {project.Name}");

        var semaphore = new SemaphoreSlim(_config.MaxConcurrency);
        _docCache.TryGetValue(projectFilePath, out var docCache);
        var documentTasks = documents.Select(async doc =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await AnalyzeDocumentAsync(doc, docCache, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();

        var documentResults = await Task.WhenAll(documentTasks);
        _docCache.TryAdd(projectFilePath, documentResults.OfType<DocumentAnalysisResult>().ToArray());
        // 合并结果
        foreach (var result in documentResults.Where(r => r != null).OfType<DocumentAnalysisResult>())
        {
            MergeDocumentResult(info, result);
        }

        // 计算项目统计
        CalculateProjectStatistics(info);
        info.GenerationTime = stopwatch.Elapsed;
        info.Conventions = DetectProjectConventions(info, project);
        stopwatch.Stop();
        return info;
    }

    private static ConventionInfo DetectProjectConventions(ProjectInfo info, Microsoft.CodeAnalysis.Project project)
    {
        var conv = new ConventionInfo();

        // 1) .editorconfig
        var rootDir = info.FullRootDir;
        var editorConfigPath = FindUpward(rootDir, ".editorconfig", maxDepth: 6);
        if (editorConfigPath != null)
        {
            conv.HasEditorConfig = true;
            conv.EditorConfigPath = editorConfigPath;
        }

        // 2) Nullable / ImplicitUsings (从 compilation options 或 parse options 能部分推断)
        // Roslyn 不直接暴露 csproj 属性，这里仅做启发式：查看是否引用 System.Runtime 且语言版本>=8 等没意义
        // 更稳妥：读取 csproj 文本（仍属于项目感知）
        TryDetectFromCsproj(info.ProjectFilePath, conv);

        // 3) test framework hint
        conv.TestFrameworkHint = GuessTestFramework(info);

        // 4) notable docs
        var candidates = new[] { "README.md", "readme.md", "docs", "ADR", "adr" };
        foreach (var c in candidates)
        {
            var path = Path.Combine(rootDir, c);
            if (File.Exists(path) || Directory.Exists(path))
                conv.NotableFiles.Add(path);
        }

        // 5) namespace style：从抽样类型的 Namespace 前缀推断
        var ns = info.Namespaces
            .Select(n => n.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n) && n != "<Global>")
            .OrderByDescending(n => n.Length)
            .FirstOrDefault();
        conv.DefaultNamespaceStyle = ns;

        return conv;
    }

    private static void TryDetectFromCsproj(string csprojPath, ConventionInfo conv)
    {
        if (!File.Exists(csprojPath)) return;

        var text = File.ReadAllText(csprojPath);

        conv.UsesNullable = text.Contains("<Nullable>enable</Nullable>", StringComparison.OrdinalIgnoreCase);
        conv.UsesImplicitUsings =
            text.Contains("<ImplicitUsings>enable</ImplicitUsings>", StringComparison.OrdinalIgnoreCase);
    }

    private static string GuessTestFramework(ProjectInfo info)
    {
        var pkgs = info.PackageReferences.Select(p => p.Name).ToList();
        if (pkgs.Any(n => n.Contains("xunit", StringComparison.OrdinalIgnoreCase))) return "xUnit";
        if (pkgs.Any(n => n.Contains("nunit", StringComparison.OrdinalIgnoreCase))) return "NUnit";
        if (pkgs.Any(n => n.Contains("mstest", StringComparison.OrdinalIgnoreCase))) return "MSTest";
        return "Unknown";
    }

    private static string? FindUpward(string startDir, string fileName, int maxDepth)
    {
        var dir = new DirectoryInfo(startDir);
        for (var i = 0; i <= maxDepth && dir != null; i++)
        {
            var candidate = Path.Combine(dir.FullName, fileName);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        return null;
    }

    private static void ExtractTargetFrameworks(Microsoft.CodeAnalysis.Project project, ProjectInfo info)
    {
        // 从程序集引用推断目标框架
        var frameworks = new HashSet<string>();

        foreach (var reference in project.MetadataReferences.OfType<PortableExecutableReference>())
        {
            if (string.IsNullOrEmpty(reference.FilePath))
            {
                continue;
            }

            var fileName = Path.GetFileNameWithoutExtension(reference.FilePath);

            if (fileName.StartsWith("netstandard"))
                frameworks.Add(fileName);
            else if (fileName.Contains(".NETCore.App"))
                frameworks.Add("net" + ExtractVersionFromPath(reference.FilePath));
            else if (fileName.Contains(".NETFramework"))
                frameworks.Add("net" + ExtractFrameworkVersion(reference.FilePath));
        }

        info.Language = project.Language;
        info.LanguageVersion =
            (project.ParseOptions as CSharpParseOptions)?.LanguageVersion.ToDisplayString() ?? "latest";
        info.TargetFrameworks = frameworks.ToList();
    }

    private static string ExtractVersionFromPath(string path)
    {
        // 提取类似 "6.0.0" 的版本号
        var match = MyRegex().Match(path);
        return match.Success ? match.Groups[1].Value : "core";
    }

    private static string ExtractFrameworkVersion(string path)
    {
        var match = System.Text.RegularExpressions.Regex.Match(path, @"v(\d+\.\d+)");
        return match.Success ? match.Groups[1].Value.Replace(".", "") : "framework";
    }

    private static void ExtractPackageReferences(Microsoft.CodeAnalysis.Project project, ProjectInfo info)
    {
        var packages = new Dictionary<string, string>();

        foreach (var reference in project.MetadataReferences.OfType<PortableExecutableReference>())
        {
            if (reference.FilePath?.Contains("packages", StringComparison.OrdinalIgnoreCase) == true ||
                reference.FilePath?.Contains("nuget", StringComparison.OrdinalIgnoreCase) == true)
            {
                var packageRef = ParsePackageReference(reference.FilePath);
                if (packageRef != null && !packages.ContainsKey(packageRef.Name))
                {
                    packages[packageRef.Name] = packageRef.Version;
                }
            }
        }

        info.PackageReferences = packages
            .Select(kvp => new PackageReference { Name = kvp.Key, Version = kvp.Value })
            .OrderBy(p => p.Name)
            .ToList();
    }

    private static PackageReference? ParsePackageReference(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        try
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            var parts = path.Split(Path.DirectorySeparatorChar);

            // 查找包名和版本号模式
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(parts[i + 1], @"^\d+\.\d+"))
                {
                    return new PackageReference
                    {
                        Name = parts[i],
                        Version = parts[i + 1]
                    };
                }
            }

            return new PackageReference { Name = fileName, Version = "Unknown" };
        }
        catch
        {
            return null;
        }
    }

    private static void ExtractProjectReferences(Microsoft.CodeAnalysis.Project project, ProjectInfo info)
    {
        info.ProjectReferences = project.ProjectReferences
            .Select(pr =>
            {
                var referencedProject = project.Solution.GetProject(pr.ProjectId);
                return referencedProject != null
                    ? new ProjectReference
                    {
                        ProjectName = referencedProject.Name,
                        ProjectPath = referencedProject.FilePath ?? string.Empty,
                    }
                    : null;
            })
            .Where(pr => pr != null)
            .OfType<ProjectReference>()
            .ToList();
    }

    private bool ShouldIncludeFile(Document document)
    {
        if (document.FilePath == null) return false;

        // 排除生成的文件
        if (document.FilePath.Contains(".g.cs") ||
            document.FilePath.Contains(".Generated.cs") ||
            document.FilePath.Contains("TemporaryGeneratedFile"))
            return false;

        // 排除 AssemblyInfo
        if (document.Name == "AssemblyInfo.cs" ||
            document.Name == "GlobalSuppressions.cs")
            return false;

        // 检查排除模式
        foreach (var pattern in _config.ExcludedPatterns)
        {
            if (document.FilePath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private async Task<DocumentAnalysisResult?> AnalyzeDocumentAsync(
        Document document, IList<DocumentAnalysisResult>? cachedResults,
        CancellationToken cancellationToken)
    {
        
        var documentFilePath = document.FilePath;
        if (documentFilePath == null)
        {
            return null;
        }

        var fileInfo = new FileInfo(documentFilePath);
        if (!fileInfo.Exists)
        {
            return null;
        }

        DocumentAnalysisResult? result;
        if ((result = cachedResults?.FirstOrDefault(cachedResult =>
                cachedResult.FilePath == documentFilePath &&
                cachedResult.SourceEditTime == fileInfo.LastWriteTimeUtc)) != null)
        {
            return result;
        }

        IndexService.InvalidateByFile(documentFilePath);

        var relativePath = document.Folders.Count > 0
            ? Path.Combine(Path.Combine(document.Folders.ToArray()), document.Name)
            : document.Name;
        try
        {
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
            if (syntaxRoot == null) return null;
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (semanticModel == null) return null;
            result = new DocumentAnalysisResult
            {
                FilePath = documentFilePath,
                LinesOfCode = syntaxRoot.GetText().Lines.Count,
                SourceEditTime = fileInfo.LastWriteTimeUtc,
                FileEntry = new FileEntryInfo
                {
                    FilePath = documentFilePath,
                    RelativePath = relativePath,
                    ProjectFilePath = document.Project.FilePath ?? string.Empty,
                    Extension = Path.GetExtension(documentFilePath),
                    SizeBytes = fileInfo.Length,
                    LinesOfCode = syntaxRoot.GetText().Lines.Count,
                    LastWriteTimeUtc = fileInfo.LastWriteTimeUtc,
                    Kind = InferFileKind(documentFilePath)
                }
            };

            // 提取所有命名空间（包括文件范围命名空间）
            var namespaceDeclarations = syntaxRoot.DescendantNodes()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .ToList();

            // 处理顶层程序
            if (!namespaceDeclarations.Any())
            {
                var globalNamespace = new NamespaceInfo
                {
                    Name = "<Global>",
                    FilePath = documentFilePath
                };

                var topLevelTypes = syntaxRoot.DescendantNodes()
                    .OfType<TypeDeclarationSyntax>();

                foreach (var type in topLevelTypes)
                {
                    var typeInfo = ExtractTypeInfo(type, semanticModel);
                    if (typeInfo != null)
                    {
                        globalNamespace.Types.Add(typeInfo);
                        result.TypeCount++;
                    }
                }

                if (globalNamespace.Types.Any())
                    result.Namespaces.Add(globalNamespace);
            }
            else
            {
                foreach (var nsDecl in namespaceDeclarations)
                {
                    var nsInfo = new NamespaceInfo
                    {
                        Name = nsDecl.Name.ToString(),
                        FilePath = documentFilePath
                    };

                    var types = nsDecl.DescendantNodes().OfType<TypeDeclarationSyntax>();

                    foreach (var type in types)
                    {
                        if (!IsAccessible(type.Modifiers) && !_config.IncludePrivateMembers)
                            continue;

                        var typeInfo = ExtractTypeInfo(type, semanticModel);
                        if (typeInfo != null)
                        {
                            nsInfo.Types.Add(typeInfo);
                            result.TypeCount++;
                            result.MethodCount += typeInfo.Members.Count(m => m.Kind == "Method");
                        }
                    }

                    if (nsInfo.Types.Any())
                        result.Namespaces.Add(nsInfo);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error analyzing document {documentFilePath}: {ex.Message}");
            return null;
        }
    }

    private static string InferFileKind(string path)
    {
        var name = Path.GetFileName(path);

        if (name.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            return "Generated";

        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".cs" => "Source",
            ".json" or ".xml" or ".config" => "Config",
            ".md" or ".txt" => "Doc",
            ".resx" => "Resource",
            _ => "Other"
        };
    }


    private bool ShouldIncludeMember(MemberDeclarationSyntax member, SemanticModel semanticModel)
    {
        // 跳过私有成员（除非配置要求包含）
        if (!_config.IncludePrivateMembers && !IsAccessible(member.Modifiers))
            return false;

        // 跳过编译器生成的成员
        if (member.AttributeLists.Any(al => al.Attributes.Any(a =>
                a.Name.ToString().Contains("CompilerGenerated"))))
            return false;

        // 跳过简单的属性访问器
        if (member is MethodDeclarationSyntax method)
        {
            if (IsSimplePropertyAccessor(method, semanticModel)) return false;
            // 对于转发方法，根据配置决定是否包含
            if (!_config.IncludeForwardingMethods && IsForwardingMethod(method))
                return false;
        }

        return true;
    }

    private TypeInfo? ExtractTypeInfo(
        TypeDeclarationSyntax typeDecl,
        SemanticModel semanticModel)
    {
        var typeInfo = _mapper.Map<TypeInfo?>(typeDecl, opts => opts.Items["SemanticModel"] = semanticModel);
        if (typeInfo == null)
        {
            return null;
        }

        foreach (var member in typeDecl.Members)
        {
            if (ShouldIncludeMember(member, semanticModel))
            {
                var symbolInfo = _mapper.Map<SymbolInfo?>(
                    member, opts => opts.Items["SemanticModel"] = semanticModel
                );
                if (symbolInfo is MemberInfo memberInfo)
                {
                    IndexService.AddSymbol(memberInfo);
                    typeInfo.Members.Add(memberInfo);
                }
            }
        }

        return typeInfo;
    }

    private static bool IsAccessible(SyntaxTokenList modifiers)
    {
        return modifiers.Any(m =>
            m.IsKind(SyntaxKind.PublicKeyword) ||
            m.IsKind(SyntaxKind.InternalKeyword) ||
            m.IsKind(SyntaxKind.ProtectedKeyword));
    }

    private static bool IsSimplePropertyAccessor(MethodDeclarationSyntax syntax, SemanticModel semanticModel)
    {
        var symbol = semanticModel.GetDeclaredSymbol(syntax);
        return symbol?.MethodKind switch
        {
            MethodKind.PropertyGet => true,
            MethodKind.PropertySet => true,
            _ => false
        };
    }

    private static bool IsForwardingMethod(MethodDeclarationSyntax method)
    {
        // 表达式体方法且只是简单调用
        if (method.ExpressionBody?.Expression is InvocationExpressionSyntax invocation)
        {
            return invocation.Expression is MemberAccessExpressionSyntax;
        }

        // 方法体只有一条 return 语句
        if (method.Body?.Statements.Count == 1)
        {
            var stmt = method.Body.Statements[0];
            if (stmt is ReturnStatementSyntax returnStmt &&
                returnStmt.Expression is InvocationExpressionSyntax)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPublicApi(Microsoft.CodeAnalysis.Accessibility accessibility)
    {
        return accessibility == Microsoft.CodeAnalysis.Accessibility.Public ||
               accessibility == Microsoft.CodeAnalysis.Accessibility.Protected ||
               (_config.IncludeInternalMembers && accessibility == Microsoft.CodeAnalysis.Accessibility.Internal);
    }


    private static void MergeDocumentResult(ProjectInfo projectInfo, DocumentAnalysisResult result)
    {
        foreach (var namespaceInfo in result.Namespaces)
        {
            var existingNs = projectInfo.Namespaces.FirstOrDefault(n => n.Name == namespaceInfo.Name);
            if (existingNs != null)
            {
                existingNs.Types.AddRange(namespaceInfo.Types);
            }
            else
            {
                //浅拷贝
                var info = new NamespaceInfo()
                {
                    Name = namespaceInfo.Name,
                    FilePath = namespaceInfo.FilePath,
                    Types = namespaceInfo.Types.ToList(),
                };
                projectInfo.Namespaces.Add(info);
            }
        }

        if (result.FileEntry != null)
        {
            // 去重：按 RelativePath 或 FilePath
            if (!projectInfo.Files.Any(f =>
                    string.Equals(f.FilePath, result.FileEntry.FilePath, StringComparison.OrdinalIgnoreCase)))
            {
                projectInfo.Files.Add(result.FileEntry);
            }
        }

        projectInfo.Statistics.FilesCount++;
        projectInfo.Statistics.LinesOfCode += result.LinesOfCode;
        projectInfo.Statistics.TypesCount += result.TypeCount;
        projectInfo.Statistics.MethodsCount += result.MethodCount;
    }

    private static void CalculateProjectStatistics(ProjectInfo project)
    {
        project.Statistics.TypesCount = project.Namespaces.Sum(ns => ns.Types.Count);
        project.Statistics.MethodsCount = project.Namespaces
            .SelectMany(ns => ns.Types)
            .SelectMany(t => t.Members)
            .Count(m => m.Kind == "Method" || m.Kind == "Constructor");
        project.Statistics.PropertyCount = project.Namespaces
            .SelectMany(ns => ns.Types)
            .SelectMany(t => t.Members)
            .Count(m => m.Kind == "Property");
    }

    private static void CalculateSummaryStatistics(SolutionInfo summary)
    {
        summary.Statistics.TotalProjects = summary.Projects.Count;
        summary.Statistics.FilesCount = summary.Projects.Sum(p => p.Statistics.FilesCount);
        summary.Statistics.TypesCount = summary.Projects.Sum(p => p.Statistics.TypesCount);
        summary.Statistics.MethodsCount = summary.Projects.Sum(p => p.Statistics.MethodsCount);
        summary.Statistics.LinesOfCode = summary.Projects.Sum(p => p.Statistics.LinesOfCode);

        // 类型分布
        summary.Statistics.TypeDistribution = summary.Projects
            .SelectMany(p => p.Namespaces)
            .SelectMany(ns => ns.Types)
            .GroupBy(t => t.Kind)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Applies Roslyn solution changes to disk via MSBuildWorkspace.TryApplyChanges,
    /// then refreshes the live <see cref="CurrentRawSolution"/> reference.
    /// </summary>
    /// <returns>True if changes were written; false if the workspace refused.</returns>
    public bool ApplySolutionChanges(Solution newSolution)
    {
        if (!IsLoaded)
            throw new InvalidOperationException("Solution is not loaded.");

        try
        {
            var success = _workspace.TryApplyChanges(newSolution);
            if (success)
            {
                CurrentRawSolution = newSolution;
                _logger?.LogInformation("Roslyn solution changes applied successfully.");
            }
            else
            {
                _logger?.LogWarning("MSBuildWorkspace.TryApplyChanges returned false; changes were not written.");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to apply solution changes: {ex.Message}");
            throw;
        }
    }

    public void CloseCurrentSolution()
    {
        if (!IsLoaded)
        {
            return;
        }

        try
        {
            _workspace.CloseSolution();
        }
        catch (Exception exception)
        {
            _logger?.LogError($"Error closing solution: {exception.Message}");
        }
        finally
        {
            CurrentRawSolution = null;
            SolutionDir = null;
            SolutionInfo = null;
        }
    }

    public void Dispose()
    {
        CloseCurrentSolution();
        _workspace.Dispose();
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"(\d+\.\d+)")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();

    internal void SetForTesting(SolutionInfo info)
    {
        SolutionInfo = info;
        CurrentRawSolution = null; // 让依赖 Roslyn 的分支走 catch/fallback
        SolutionDir = Path.GetDirectoryName(info.SolutionPath);
    }
}