using System.Collections.Concurrent;
using System.Diagnostics;
using LLMClient.Dialog;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace LLMClient.ContextEngineering.Analysis;

public partial class RoslynProjectAnalyzer : IDisposable
{
    private readonly MSBuildWorkspace _workspace;
    private readonly AnalyzerConfig _config;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, string> _xmlDocCache = new();
    private readonly ConcurrentDictionary<string, IList<DocumentAnalysisResult>> _docCache = new();
    private readonly SymbolIndexService _symbolIndexService = new();

    public RoslynProjectAnalyzer(ILogger? logger, AnalyzerConfig? config = null)
    {
        _config = config ?? new AnalyzerConfig();
        _logger = logger;
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


    public async Task<SolutionInfo> AnalyzeSolutionAsync(
        string solutionPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(solutionPath))
            throw new FileNotFoundException($"Solution file not found: {solutionPath}");

        var stopwatch = Stopwatch.StartNew();
        _logger?.LogInformation($"Starting analysis of {Path.GetFileName(solutionPath)}...");

        var summary = new SolutionInfo
        {
            SolutionName = Path.GetFileNameWithoutExtension(solutionPath),
            SolutionPath = solutionPath,
            GeneratedAt = DateTime.UtcNow
        };

        try
        {
            var solution = await _workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken);
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
            summary.Projects = results.Where(r => r != null).OfType<ProjectInfo>().ToList();

            // 计算统计信息
            CalculateSummaryStatistics(summary);

            stopwatch.Stop();
            summary.GenerationTime = stopwatch.Elapsed;
            _logger?.LogInformation($"Analysis completed in {stopwatch.ElapsedMilliseconds}ms");
            summary.Conventions = MergeSolutionConventions(summary.Projects);
            return summary;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Solution analysis failed: {ex}");
            throw;
        }
        finally
        {
            _workspace.CloseSolution();
        }
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

    public async Task<ProjectInfo> AnalyzeProjectAsync(string csprojPath)
    {
        if (!File.Exists(csprojPath))
            throw new FileNotFoundException($"Project file not found: {csprojPath}");
        var project = await _workspace.OpenProjectAsync(csprojPath);
        try
        {
            var projectInfo = await AnalyzeProjectAsync(project, CancellationToken.None);
            if (projectInfo == null)
                throw new Exception($"Failed to analyze project: {csprojPath}");
            return projectInfo;
        }
        finally
        {
            _workspace.CloseSolution();
        }
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

    private ConventionInfo DetectProjectConventions(ProjectInfo info, Microsoft.CodeAnalysis.Project project)
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

        _symbolIndexService.InvalidateByFile(documentFilePath);
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
                    var typeInfo = ExtractTypeInfo(type, semanticModel, documentFilePath, relativePath);
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

                        var typeInfo = ExtractTypeInfo(type, semanticModel, documentFilePath, relativePath);
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

    private TypeInfo? ExtractTypeInfo(
        TypeDeclarationSyntax typeDecl,
        SemanticModel semanticModel, string filePath, string relativePath)
    {
        var symbol = semanticModel.GetDeclaredSymbol(typeDecl);
        if (symbol == null) return null;

        var info = new TypeInfo
        {
            Name = symbol.Name,
            Kind = GetTypeKind(typeDecl),
            Signature = symbol.ToDisplayString(),
            Accessibility = symbol.DeclaredAccessibility.ToString(),
            Summary = GetXmlComment(typeDecl),
            FilePath = filePath,
            LineNumber = typeDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            IsPartial = typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword),
            IsAbstract = symbol.IsAbstract,
            IsSealed = symbol.IsSealed,
            // 提取属性
            Attributes = ExtractAttributes(typeDecl.AttributeLists),
            RelativePath = relativePath,
            Locations = symbol.Locations
                .Where(loc => loc.IsInSource)
                .Select(loc =>
                {
                    var lineSpan = loc.GetLineSpan(); // 获取文件与行列范围  
                    return new CodeLocation
                    {
                        FilePath = lineSpan.Path,
                        Location = new LinePositionSpan(
                            new LinePosition(
                                lineSpan.StartLinePosition.Line + 1, // 转为1基  
                                lineSpan.StartLinePosition.Character + 1
                            ),
                            new LinePosition(
                                lineSpan.EndLinePosition.Line + 1,
                                lineSpan.EndLinePosition.Character + 1
                            )
                        )
                    };
                })
                .ToList()
        };
        _symbolIndexService.AddSymbol(info);
        // 提取基类型
        if (symbol.BaseType != null && symbol.BaseType.SpecialType == SpecialType.None)
        {
            info.BaseTypes.Add(FormatTypeName(symbol.BaseType));
        }

        // 提取接口
        info.ImplementedInterfaces = symbol.Interfaces
            .Select(FormatTypeName)
            .ToList();

        // 提取成员（只提取重要成员）
        foreach (var member in typeDecl.Members)
        {
            if (ShouldIncludeMember(member))
            {
                var memberInfo = ExtractMemberInfo(member, semanticModel);
                if (memberInfo != null)
                {
                    info.Members.Add(memberInfo);
                }
            }
        }

        return info;
    }

    private static string GetTypeKind(TypeDeclarationSyntax typeDecl)
    {
        return typeDecl switch
        {
            ClassDeclarationSyntax => "Class",
            InterfaceDeclarationSyntax => "Interface",
            StructDeclarationSyntax => "Struct",
            ExtensionBlockDeclarationSyntax => "ExtensionBlock",
            // EnumDeclarationSyntax => "Enum",
            RecordDeclarationSyntax => "Record",
            _ => "Unknown"
        };
    }

    private bool ShouldIncludeMember(MemberDeclarationSyntax member)
    {
        // 跳过私有成员（除非配置要求包含）
        if (!_config.IncludePrivateMembers && !IsAccessible(member.Modifiers))
            return false;

        // 跳过编译器生成的成员
        if (member.AttributeLists.Any(al => al.Attributes.Any(a =>
                a.Name.ToString().Contains("CompilerGenerated"))))
            return false;

        return true;
    }

    private MemberInfo? ExtractMemberInfo(MemberDeclarationSyntax member, SemanticModel semanticModel)
    {
        MemberInfo? memberInfo = null;
        switch (member)
        {
            case MethodDeclarationSyntax method:
                memberInfo = ExtractMethodInfo(method, semanticModel);
                break;
            case PropertyDeclarationSyntax property:
                memberInfo = ExtractPropertyInfo(property, semanticModel);
                break;
            case FieldDeclarationSyntax field:
                memberInfo = ExtractFieldInfo(field, semanticModel);
                break;
            case ConstructorDeclarationSyntax ctor:
                memberInfo = ExtractConstructorInfo(ctor, semanticModel);
                break;
            case EventDeclarationSyntax evt:
                memberInfo = ExtractEventInfo(evt, semanticModel);
                break;
            default:
                memberInfo = null;
                break;
        }

        if (memberInfo != null)
        {
            _symbolIndexService.AddSymbol(memberInfo);
        }

        return memberInfo;
    }

    private static MemberInfo CreateMemberInfo(ISymbol symbol)
    {
        return new MemberInfo
        {
            Name = symbol.Name,
            Kind = symbol.Kind.ToString(),
            Accessibility = symbol.DeclaredAccessibility.ToString(),
            Signature = BuildSymbolSignature(symbol),
            IsStatic = symbol.IsStatic,
            IsVirtual = symbol.IsVirtual,
            IsOverride = symbol.IsOverride,
            UniqueId = symbol.GetDocumentationCommentId(),
            Locations = symbol.Locations
                .Where(loc => loc.IsInSource)
                .Select(loc =>
                {
                    var lineSpan = loc.GetLineSpan(); // 获取文件与行列范围  
                    return new CodeLocation
                    {
                        FilePath = lineSpan.Path,
                        Location = new LinePositionSpan(
                            new LinePosition(
                                lineSpan.StartLinePosition.Line + 1, // 转为1基  
                                lineSpan.StartLinePosition.Character + 1
                            ),
                            new LinePosition(
                                lineSpan.EndLinePosition.Line + 1,
                                lineSpan.EndLinePosition.Character + 1
                            )
                        )
                    };
                })
                .ToList()
        };
    }

    private MemberInfo? ExtractMethodInfo(MethodDeclarationSyntax method, SemanticModel semanticModel)
    {
        var symbol = semanticModel.GetDeclaredSymbol(method);
        if (symbol == null) return null;

        // 跳过简单的属性访问器
        if (IsSimplePropertyAccessor(method)) return null;

        // 对于转发方法，根据配置决定是否包含
        if (!_config.IncludeForwardingMethods && IsForwardingMethod(method))
            return null;
        var memberInfo = CreateMemberInfo(symbol);
        memberInfo.IsAsync = symbol.IsAsync;
        memberInfo.ReturnType = FormatTypeName(symbol.ReturnType);
        memberInfo.Parameters = symbol.Parameters.Select(p => new ParameterInfo
        {
            Name = p.Name,
            Type = FormatTypeName(p.Type),
            HasDefaultValue = p.HasExplicitDefaultValue,
            DefaultValue = p.HasExplicitDefaultValue ? p.ExplicitDefaultValue?.ToString() : null
        }).ToList();
        memberInfo.Comment = GetXmlComment(method);
        memberInfo.Attributes = ExtractAttributes(method.AttributeLists);
        return memberInfo;
    }

    private MemberInfo? ExtractPropertyInfo(PropertyDeclarationSyntax property, SemanticModel semanticModel)
    {
        var symbol = semanticModel.GetDeclaredSymbol(property);
        if (symbol == null) return null;
        var memberInfo = CreateMemberInfo(symbol);
        memberInfo.ReturnType = FormatTypeName(symbol.Type);
        memberInfo.Comment = GetXmlComment(property);
        memberInfo.Attributes = ExtractAttributes(property.AttributeLists);
        return memberInfo;
    }

    private MemberInfo? ExtractFieldInfo(FieldDeclarationSyntax field, SemanticModel semanticModel)
    {
        var variable = field.Declaration.Variables.FirstOrDefault();
        if (variable == null) return null;

        var symbol = semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
        if (symbol == null) return null;
        var memberInfo = CreateMemberInfo(symbol);
        memberInfo.ReturnType = FormatTypeName(symbol.Type);
        memberInfo.Comment = GetXmlComment(field);
        memberInfo.Attributes = ExtractAttributes(field.AttributeLists);
        return memberInfo;
    }

    private MemberInfo? ExtractConstructorInfo(ConstructorDeclarationSyntax ctor, SemanticModel semanticModel)
    {
        var symbol = semanticModel.GetDeclaredSymbol(ctor);
        if (symbol == null) return null;
        var memberInfo = CreateMemberInfo(symbol);
        memberInfo.Name = symbol.ContainingType.Name; // 构造函数显示为类型名
        memberInfo.Parameters = symbol.Parameters.Select(p => new ParameterInfo
            {
                Name = p.Name,
                Type = FormatTypeName(p.Type),
                HasDefaultValue = p.HasExplicitDefaultValue,
                DefaultValue = p.HasExplicitDefaultValue
                    ? p.ExplicitDefaultValue?.ToString()
                    : null
            })
            .ToList();
        memberInfo.Comment = GetXmlComment(ctor);
        memberInfo.Attributes = ExtractAttributes(ctor.AttributeLists);
        memberInfo.ReturnType = symbol.ReturnType.Name;
        return memberInfo;
    }

    private MemberInfo? ExtractEventInfo(EventDeclarationSyntax evt, SemanticModel semanticModel)
    {
        var symbol = semanticModel.GetDeclaredSymbol(evt);
        if (symbol == null) return null;
        var memberInfo = CreateMemberInfo(symbol);
        memberInfo.ReturnType = FormatTypeName(symbol.Type);
        memberInfo.Comment = GetXmlComment(evt);
        return memberInfo;
    }

    private static string BuildSymbolSignature(ISymbol symbol)
    {
        // 定义显示格式：这是关键
        var format = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            memberOptions: SymbolDisplayMemberOptions.IncludeParameters |
                           SymbolDisplayMemberOptions.IncludeType |
                           SymbolDisplayMemberOptions.IncludeModifiers |
                           SymbolDisplayMemberOptions.IncludeRef,
            parameterOptions: SymbolDisplayParameterOptions.IncludeType);

        // 示例输出: "public async Task<int> MyMethod(string name, int count = 0)"
        return symbol.ToDisplayString(format);
        /*var sb = new StringBuilder();
        if (symbol.ReturnsVoid)
            sb.Append("void");
        else
            sb.Append(FormatTypeName(symbol.ReturnType));

        sb.Append(' ');
        sb.Append(symbol.Name);

        if (symbol.IsGenericMethod)
        {
            sb.Append('<');
            sb.Append(string.Join(", ", symbol.TypeParameters.Select(t => t.Name)));
            sb.Append('>');
        }

        sb.Append('(');
        sb.Append(string.Join(", ", symbol.Parameters.Select(p =>
            $"{FormatTypeName(p.Type)} {p.Name}")));
        sb.Append(')');

        return sb.ToString();*/
    }

    private static string FormatTypeName(ITypeSymbol? type)
    {
        if (type == null) return "unknown";

        // 简化常见类型名称
        var displayString = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        // 移除不必要的命名空间
        displayString = displayString
            .Replace("System.Collections.Generic.", "")
            .Replace("System.Threading.Tasks.", "")
            .Replace("System.", "");

        return displayString;
    }

    private static List<string> ExtractAttributes(SyntaxList<AttributeListSyntax> attributeLists)
    {
        var attributes = new List<string>();

        foreach (var list in attributeLists)
        {
            foreach (var attr in list.Attributes)
            {
                var name = attr.Name.ToString();
                // 移除 "Attribute" 后缀
                if (name.EndsWith("Attribute"))
                    name = name.Substring(0, name.Length - 9);
                attributes.Add(name);
            }
        }

        return attributes;
    }

    private static bool IsAccessible(SyntaxTokenList modifiers)
    {
        return modifiers.Any(m =>
            m.IsKind(SyntaxKind.PublicKeyword) ||
            m.IsKind(SyntaxKind.InternalKeyword) ||
            m.IsKind(SyntaxKind.ProtectedKeyword));
    }

    private static bool IsSimplePropertyAccessor(MethodDeclarationSyntax method)
    {
        var name = method.Identifier.Text;
        return (name.StartsWith("get_") || name.StartsWith("set_")) &&
               method.Parent is TypeDeclarationSyntax;
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

    private string GetXmlComment(SyntaxNode node)
    {
        var key = node.GetLocation().ToString();

        if (_xmlDocCache.TryGetValue(key, out var cached))
            return cached;

        var trivia = node.GetLeadingTrivia()
            .Select(t => t.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .FirstOrDefault();

        if (trivia == null)
        {
            _xmlDocCache[key] = string.Empty;
            return string.Empty;
        }

        var summaryElement = trivia.Content
            .OfType<XmlElementSyntax>()
            .FirstOrDefault(e => e.StartTag.Name.ToString() == "summary");

        if (summaryElement == null)
        {
            _xmlDocCache[key] = string.Empty;
            return string.Empty;
        }

        var summary = string.Join(" ", summaryElement.Content
                .OfType<XmlTextSyntax>()
                .SelectMany(t => t.TextTokens)
                .Select(token => token.ToString().Trim()))
            .Trim();

        _xmlDocCache[key] = summary;
        return summary;
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

    public void Dispose()
    {
        _workspace?.Dispose();
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"(\d+\.\d+)")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();
}