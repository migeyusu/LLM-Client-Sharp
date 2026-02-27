using LLMClient.ContextEngineering.Analysis;
using LLMClient.ContextEngineering.Tools.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using ProjectInfo = LLMClient.ContextEngineering.Analysis.ProjectInfo;
using SolutionInfo = LLMClient.ContextEngineering.Analysis.SolutionInfo;
using SymbolInfo = LLMClient.ContextEngineering.Analysis.SymbolInfo;
using TypeInfo = LLMClient.ContextEngineering.Analysis.TypeInfo;

namespace LLMClient.ContextEngineering.Tools;

public sealed class SymbolSemanticService
{
    private readonly SolutionContext _context;
    private readonly ILogger<SymbolSemanticService>? _logger;

    // 硬性上限，防止 LLM 被海量数据淹没
    private const int MaxCallers = 50;
    private const int MaxCallees = 100;
    private const int MaxUsages = 200;
    private const int MaxDerivedTypes = 50;

    public SymbolSemanticService(SolutionContext context, ILogger<SymbolSemanticService>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    // ── search_symbols ────────────────────────────────────────────────────

    public List<SymbolSearchResult> SearchSymbols(
        string query,
        string? kind = null,
        string? scope = null,
        int topK = 20)
    {
        var info = _context.RequireSolutionInfo();
        topK = Math.Clamp(topK, 1, 100);

        var scored = new List<(SymbolInfo sym, double score)>();

        foreach (var sym in EnumerateAllSymbols(info))
        {
            // kind 过滤（大小写不敏感）
            if (!string.IsNullOrWhiteSpace(kind) &&
                !sym.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase))
                continue;

            // scope 过滤：匹配文件路径片段 或 签名前缀（命名空间）
            if (!string.IsNullOrWhiteSpace(scope) &&
                !sym.FilesPath.Any(fp => fp.Contains(scope, StringComparison.OrdinalIgnoreCase)) &&
                !sym.Signature.Contains(scope, StringComparison.OrdinalIgnoreCase))
                continue;

            var score = ScoreSymbol(sym, query);
            if (score > 0)
                scored.Add((sym, score));
        }

        return scored
            .OrderByDescending(x => x.score)
            .Take(topK)
            .Select(x => ToSearchResult(x.sym, x.score, info))
            .ToList();
    }

    private static double ScoreSymbol(SymbolInfo sym, string query)
    {
        // 精确名称匹配权重最高，向下依次降级
        if (sym.Name.Equals(query, StringComparison.OrdinalIgnoreCase)) return 1.0;
        if (sym.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 0.85;
        if (sym.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) return 0.70;
        if (sym.Signature.Contains(query, StringComparison.OrdinalIgnoreCase)) return 0.45;

        var summary = sym is MemberInfo mi ? mi.Comment : sym.Summary;
        if (summary?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) return 0.30;

        return 0;
    }

    // ── get_symbol_detail ─────────────────────────────────────────────────

    public SymbolDetailView GetSymbolDetail(string symbolId)
    {
        var info = _context.RequireSolutionInfo();
        var sym = ResolveSymbol(symbolId, info)
                  ?? throw new ArgumentException(
                      $"Symbol '{symbolId}' not found. Use search_symbols to discover valid IDs.");

        var (containingType, containingNs) = ResolveContaining(sym, info);

        var view = new SymbolDetailView
        {
            SymbolId = GetId(sym),
            Name = sym.Name,
            Kind = sym.Kind,
            Signature = sym.Signature,
            Accessibility = sym.Accessibility,
            Summary = sym is MemberInfo m0 ? m0.Comment ?? sym.Summary : sym.Summary,
            Attributes = sym.Attributes.ToList(),
            Locations = MapLocations(sym.Locations)
        };

        if (sym is TypeInfo ti)
        {
            return view with
            {
                TypeDetail = new TypeDetailExtra
                {
                    BaseTypes = ti.BaseTypes.ToList(),
                    ImplementedInterfaces = ti.ImplementedInterfaces.ToList(),
                    IsPartial = ti.IsPartial,
                    IsAbstract = ti.IsAbstract,
                    IsSealed = ti.IsSealed,
                    MemberCount = ti.Members.Count
                }
            };
        }

        if (sym is MemberInfo mi)
        {
            return view with
            {
                MemberDetail = new MemberDetailExtra
                {
                    ReturnType = mi.ReturnType,
                    Parameters = mi.Parameters?.Select(p => new ParameterView
                    {
                        Name = p.Name,
                        Type = p.Type,
                        DefaultValue = p.DefaultValue
                    }).ToList(),
                    IsStatic = mi.IsStatic,
                    IsAsync = mi.IsAsync,
                    IsVirtual = mi.IsVirtual,
                    IsOverride = mi.IsOverride,
                    ContainingType = containingType
                }
            };
        }

        return view;
    }

    // ── get_type_members ──────────────────────────────────────────────────

    public TypeMembersView GetTypeMembers(
        string typeId,
        string? kindFilter = null,
        string? accessibilityFilter = null)
    {
        var info = _context.RequireSolutionInfo();
        var typeInfo = ResolveType(typeId, info)
                       ?? throw new ArgumentException($"Type '{typeId}' not found.");

        var members = typeInfo.Members.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(kindFilter))
            members = members.Where(m =>
                m.Kind.Equals(kindFilter, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(accessibilityFilter))
            members = members.Where(m =>
                m.Accessibility.Contains(accessibilityFilter, StringComparison.OrdinalIgnoreCase));

        var list = members.ToList();

        return new TypeMembersView
        {
            TypeId = GetId(typeInfo),
            TypeName = typeInfo.Name,
            TypeSignature = typeInfo.Signature,
            TotalCount = list.Count,
            Members = list.Select(m => new MemberSummaryView
            {
                SymbolId = GetId(m),
                Name = m.Name,
                Kind = m.Kind,
                Signature = m.Signature,
                Accessibility = m.Accessibility,
                ReturnType = m.ReturnType,
                Summary = m.Comment,
                IsStatic = m.IsStatic,
                IsAsync = m.IsAsync,
                Location = MapLocations(m.Locations).FirstOrDefault()
            }).ToList()
        };
    }

    // ── get_type_hierarchy ────────────────────────────────────────────────

    public async Task<TypeHierarchyView> GetTypeHierarchyAsync(
        string typeId,
        CancellationToken ct = default)
    {
        var info = _context.RequireSolutionInfo();
        var typeInfo = ResolveType(typeId, info)
                       ?? throw new ArgumentException($"Type '{typeId}' not found.");

        var baseView = new TypeHierarchyView
        {
            TypeId = GetId(typeInfo),
            TypeName = typeInfo.Name,
            TypeSignature = typeInfo.Signature,
            BaseChain = typeInfo.BaseTypes.ToList(),
            ImplementedInterfaces = typeInfo.ImplementedInterfaces.ToList()
        };

        // 优先使用 Roslyn SymbolFinder（精确）
        try
        {
            var solution = _context.RequireRoslynSolution();
            var roslynType = await ResolveRoslynNamedTypeAsync(typeInfo, solution, ct);
            if (roslynType != null)
            {
                IEnumerable<INamedTypeSymbol> derived;
                if (roslynType.TypeKind == TypeKind.Interface)
                    derived = await SymbolFinder.FindImplementationsAsync(roslynType, solution, cancellationToken: ct);
                else
                    derived = await SymbolFinder.FindDerivedClassesAsync(roslynType, solution, cancellationToken: ct);

                return baseView with
                {
                    DerivedTypes = derived.Take(MaxDerivedTypes).Select(ToBrief).ToList(),
                    Source = "Roslyn"
                };
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("SymbolFinder.FindDerivedClasses failed, falling back to index: {Msg}", ex.Message);
        }

        // 静态索引兜底
        return baseView with
        {
            DerivedTypes = FindDerivedInIndex(typeInfo.Name, info),
            Source = "Index"
        };
    }

    // ── get_interface_implementations ─────────────────────────────────────

    public async Task<ImplementationsView> GetInterfaceImplementationsAsync(
        string interfaceId,
        CancellationToken ct = default)
    {
        var info = _context.RequireSolutionInfo();
        var typeInfo = ResolveType(interfaceId, info)
                       ?? throw new ArgumentException($"Interface '{interfaceId}' not found.");

        try
        {
            var solution = _context.RequireRoslynSolution();
            var roslynType = await ResolveRoslynNamedTypeAsync(typeInfo, solution, ct);
            if (roslynType != null)
            {
                var impls = await SymbolFinder.FindImplementationsAsync(roslynType, solution, cancellationToken: ct);
                return new ImplementationsView
                {
                    InterfaceId = GetId(typeInfo),
                    InterfaceName = typeInfo.Name,
                    Implementations = impls.Take(100).Select(ToBrief).ToList(),
                    Source = "Roslyn"
                };
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("FindImplementations failed, using index: {Msg}", ex.Message);
        }

        return new ImplementationsView
        {
            InterfaceId = GetId(typeInfo),
            InterfaceName = typeInfo.Name,
            Implementations = FindImplementationsInIndex(typeInfo.Name, info),
            Source = "Index"
        };
    }

    // ── get_callers ────────────────────────────────────────────────────────

    public async Task<CallersView> GetCallersAsync(
        string symbolId,
        string? scope = null,
        CancellationToken ct = default)
    {
        var info = _context.RequireSolutionInfo();
        var sym = ResolveSymbol(symbolId, info)
                  ?? throw new ArgumentException($"Symbol '{symbolId}' not found.");

        var solution = _context.RequireRoslynSolution();
        var roslynSym = await ResolveRoslynSymbolAsync(sym, solution, ct);

        if (roslynSym == null)
        {
            _logger?.LogWarning("Could not resolve Roslyn symbol for '{Id}'", symbolId);
            return new CallersView { SymbolId = GetId(sym), SymbolName = sym.Name };
        }

        var callerInfos = await SymbolFinder.FindCallersAsync(roslynSym, solution, ct);

        var callers = callerInfos
            .Where(ci => string.IsNullOrWhiteSpace(scope) ||
                         ci.CallingSymbol.Locations
                             .Any(l => l.IsInSource &&
                                       l.GetLineSpan().Path
                                        .Contains(scope, StringComparison.OrdinalIgnoreCase)))
            .Take(MaxCallers)
            .Select(ci => new CallerView
            {
                CallerSymbolId = ci.CallingSymbol.GetDocumentationCommentId()
                                 ?? ci.CallingSymbol.ToDisplayString(),
                CallerName = ci.CallingSymbol.Name,
                CallerSignature = ci.CallingSymbol.ToDisplayString(
                    SymbolDisplayFormat.MinimallyQualifiedFormat),
                CallSites = ci.Locations.Select(loc =>
                {
                    var span = loc.GetLineSpan();
                    return new LocationView
                    {
                        FilePath = span.Path,
                        StartLine = span.StartLinePosition.Line + 1,
                        EndLine = span.EndLinePosition.Line + 1
                    };
                }).ToList()
            })
            .ToList();

        return new CallersView
        {
            SymbolId = GetId(sym),
            SymbolName = sym.Name,
            TotalCallers = callers.Count,
            Callers = callers
        };
    }

    // ── get_callees ────────────────────────────────────────────────────────

    public async Task<CalleesView> GetCalleesAsync(
        string symbolId,
        CancellationToken ct = default)
    {
        var info = _context.RequireSolutionInfo();
        var sym = ResolveSymbol(symbolId, info)
                  ?? throw new ArgumentException($"Symbol '{symbolId}' not found.");

        var solution = _context.RequireRoslynSolution();
        var roslynSym = await ResolveRoslynSymbolAsync(sym, solution, ct);

        if (roslynSym == null)
            return new CalleesView { SymbolId = GetId(sym), SymbolName = sym.Name };

        var callees = new List<SymbolBriefView>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // 取第一个源码位置所在的文档进行语法树遍历
        var sourceLoc = roslynSym.Locations.FirstOrDefault(l => l.IsInSource);
        if (sourceLoc == null)
            return new CalleesView { SymbolId = GetId(sym), SymbolName = sym.Name };

        var doc = solution.GetDocument(sourceLoc.SourceTree);
        if (doc == null)
            return new CalleesView { SymbolId = GetId(sym), SymbolName = sym.Name };

        var semanticModel = await doc.GetSemanticModelAsync(ct);
        var root = await sourceLoc.SourceTree!.GetRootAsync(ct);
        var bodyNode = root.FindNode(sourceLoc.SourceSpan);

        // 收集调用和对象创建
        foreach (var invocation in bodyNode.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var invokedSym = semanticModel?.GetSymbolInfo(invocation, ct).Symbol;
            AddCalleeIfNew(invokedSym, seen, callees);
            if (callees.Count >= MaxCallees) break;
        }

        if (callees.Count < MaxCallees)
        {
            foreach (var creation in bodyNode.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var ctorSym = semanticModel?.GetSymbolInfo(creation, ct).Symbol;
                AddCalleeIfNew(ctorSym, seen, callees);
                if (callees.Count >= MaxCallees) break;
            }
        }

        return new CalleesView
        {
            SymbolId = GetId(sym),
            SymbolName = sym.Name,
            Callees = callees
        };
    }

    // ── get_usages ─────────────────────────────────────────────────────────

    public async Task<UsagesView> GetUsagesAsync(
        string symbolId,
        CancellationToken ct = default)
    {
        var info = _context.RequireSolutionInfo();
        var sym = ResolveSymbol(symbolId, info)
                  ?? throw new ArgumentException($"Symbol '{symbolId}' not found.");

        var solution = _context.RequireRoslynSolution();
        var roslynSym = await ResolveRoslynSymbolAsync(sym, solution, ct);

        if (roslynSym == null)
            return new UsagesView { SymbolId = GetId(sym), SymbolName = sym.Name };

        var references = await SymbolFinder.FindReferencesAsync(roslynSym, solution, ct);

        var usages = new List<UsageView>();
        foreach (var refGroup in references)
        {
            foreach (var refLoc in refGroup.Locations)
            {
                
                if (!refLoc.Location.IsInSource) continue;
                
                var lineSpan = refLoc.Location.GetLineSpan();
                var snippet = await GetSnippetAsync(solution, refLoc, ct);

                usages.Add(new UsageView
                {
                    FilePath = lineSpan.Path,
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1,
                    Snippet = snippet,
                    UsageKind = refLoc.IsWrittenTo ? "Write"
                        : refLoc.IsImplicit ? "Implicit"
                        : "Read"
                });

                if (usages.Count >= MaxUsages + 1) goto done;
            }
        }

        done:
        var truncated = usages.Count > MaxUsages;
        return new UsagesView
        {
            SymbolId = GetId(sym),
            SymbolName = sym.Name,
            TotalUsages = usages.Count,
            Truncated = truncated,
            Usages = usages.Take(MaxUsages)
                .OrderBy(u => u.FilePath)
                .ThenBy(u => u.Line)
                .ToList()
        };
    }

    // ── get_dependency_graph ───────────────────────────────────────────────

    public DependencyGraphView GetDependencyGraph(string? projectName = null, int depth = 2)
    {
        var info = _context.RequireSolutionInfo();
        depth = Math.Clamp(depth, 1, 5);

        var roots = string.IsNullOrWhiteSpace(projectName)
            ? info.Projects
            : info.Projects
                .Where(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (!roots.Any() && !string.IsNullOrWhiteSpace(projectName))
            throw new ArgumentException($"Project '{projectName}' not found.");

        var nodes = new Dictionary<string, DependencyNode>(StringComparer.Ordinal);
        var edges = new List<DependencyEdge>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void Walk(ProjectInfo p, int d)
        {
            if (d > depth || !visited.Add(p.ProjectId)) return;

            nodes.TryAdd(p.ProjectId, new DependencyNode
                { Id = p.ProjectId, Name = p.Name, Kind = "Project" });

            // 项目引用
            foreach (var pr in p.ProjectReferences)
            {
                var dep = info.Projects.FirstOrDefault(x =>
                    x.Name.Equals(pr.ProjectName, StringComparison.OrdinalIgnoreCase));
                if (dep == null) continue;

                nodes.TryAdd(dep.ProjectId, new DependencyNode
                    { Id = dep.ProjectId, Name = dep.Name, Kind = "Project" });
                edges.Add(new DependencyEdge
                    { From = p.ProjectId, To = dep.ProjectId, Kind = "ProjectReference" });

                Walk(dep, d + 1);
            }

            // 包引用（仅第一层，避免图膨胀）
            if (d == 0)
            {
                foreach (var pkg in p.PackageReferences)
                {
                    var pkgId = $"pkg:{pkg.Name}";
                    nodes.TryAdd(pkgId, new DependencyNode
                        { Id = pkgId, Name = $"{pkg.Name} ({pkg.Version})", Kind = "Package" });
                    edges.Add(new DependencyEdge
                        { From = p.ProjectId, To = pkgId, Kind = "PackageReference" });
                }
            }
        }

        foreach (var root in roots) Walk(root, 0);

        return new DependencyGraphView
        {
            ScopeLabel = projectName ?? info.SolutionName,
            Nodes = nodes.Values.ToList(),
            Edges = edges
        };
    }

    // ── get_namespace_types ────────────────────────────────────────────────

    public NamespaceTypesView GetNamespaceTypes(string namespaceName, bool includeSubNamespaces = true)
    {
        var info = _context.RequireSolutionInfo();

        var types = info.Projects
            .SelectMany(p => p.Namespaces)
            .Where(n => includeSubNamespaces
                ? n.Name.Equals(namespaceName, StringComparison.OrdinalIgnoreCase) ||
                  n.Name.StartsWith(namespaceName + ".", StringComparison.OrdinalIgnoreCase)
                : n.Name.Equals(namespaceName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(n => n.Types)
            .ToList();

        return new NamespaceTypesView
        {
            Namespace = namespaceName,
            IncludesSubNamespaces = includeSubNamespaces,
            TotalCount = types.Count,
            Types = types.Select(t => new TypeSummaryView
                {
                    SymbolId = GetId(t),
                    Name = t.Name,
                    Kind = t.Kind,
                    Signature = t.Signature,
                    Accessibility = t.Accessibility,
                    Summary = t.Summary,
                    MemberCount = t.Members.Count,
                    Location = MapLocations(t.Locations).FirstOrDefault()
                })
                .OrderBy(t => t.Kind)
                .ThenBy(t => t.Name)
                .ToList()
        };
    }

    // ── Roslyn 符号解析 ────────────────────────────────────────────────────

    private static async Task<ISymbol?> ResolveRoslynSymbolAsync(
        SymbolInfo sym,
        Solution solution,
        CancellationToken ct)
    {
        if (sym.UniqueId == null) return null;

        foreach (var project in solution.Projects)
        {
            var comp = await project.GetCompilationAsync(ct);
            if (comp == null) continue;

            var symbols = DocumentationCommentId.GetSymbolsForDeclarationId(sym.UniqueId, comp);
            var found = symbols.FirstOrDefault();
            if (found != null) return found;
        }

        return null;
    }

    private static async Task<INamedTypeSymbol?> ResolveRoslynNamedTypeAsync(
        TypeInfo typeInfo,
        Solution solution,
        CancellationToken ct)
    {
        var sym = await ResolveRoslynSymbolAsync(typeInfo, solution, ct);
        if (sym is INamedTypeSymbol named) return named;

        // 二次尝试：通过元数据名（命名空间.类名）查找
        foreach (var project in solution.Projects)
        {
            var comp = await project.GetCompilationAsync(ct);
            if (comp == null) continue;

            // 从签名中提取最简名称（命名空间限定）
            var byMeta = comp.GetTypeByMetadataName(typeInfo.Signature
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Last());
            if (byMeta != null) return byMeta;
        }

        return null;
    }

    // ── 通用 helpers ────────────────────────────────────────────────────────

    private static IEnumerable<SymbolInfo> EnumerateAllSymbols(SolutionInfo info)
    {
        foreach (var project in info.Projects)
        foreach (var ns in project.Namespaces)
        foreach (var type in ns.Types)
        {
            yield return type;
            foreach (var member in type.Members)
                yield return member;
        }
    }

    /// <summary>
    /// 按 symbolId 定位符号。优先精确 UniqueId/Signature 匹配，次之名称匹配。
    /// </summary>
    private static SymbolInfo? ResolveSymbol(string symbolId, SolutionInfo info)
    {
        SymbolInfo? nameMatch = null;

        foreach (var sym in EnumerateAllSymbols(info))
        {
            var id = GetId(sym);
            if (id == symbolId) return sym; // 精确命中

            if (nameMatch == null &&
                sym.Name.Equals(symbolId, StringComparison.OrdinalIgnoreCase))
                nameMatch = sym;
        }

        return nameMatch;
    }

    private static TypeInfo? ResolveType(string typeId, SolutionInfo info)
    {
        TypeInfo? nameMatch = null;

        foreach (var project in info.Projects)
        foreach (var ns in project.Namespaces)
        foreach (var type in ns.Types)
        {
            if (GetId(type) == typeId) return type;

            if (nameMatch == null &&
                type.Name.Equals(typeId, StringComparison.OrdinalIgnoreCase))
                nameMatch = type;
        }

        return nameMatch;
    }

    private static (string? containingType, string? containingNs) ResolveContaining(
        SymbolInfo sym, SolutionInfo info)
    {
        var id = GetId(sym);
        foreach (var project in info.Projects)
        foreach (var ns in project.Namespaces)
        foreach (var type in ns.Types)
        {
            if (GetId(type) == id) return (null, ns.Name);
            if (type.Members.Any(m => GetId(m) == id)) return (type.Name, ns.Name);
        }

        return (null, null);
    }

    private static List<LocationView> MapLocations(IEnumerable<CodeLocation> locs)
        => locs.Select(l => new LocationView
        {
            FilePath = l.FilePath,
            StartLine = l.Location.Start.Line,
            EndLine = l.Location.End.Line
        }).ToList();

    private static SymbolBriefView ToBrief(INamedTypeSymbol sym)
        => ToBrief((ISymbol)sym);

    private static SymbolBriefView ToBrief(ISymbol sym)
    {
        var loc = sym.Locations.FirstOrDefault(l => l.IsInSource);
        LocationView? locView = null;
        if (loc != null)
        {
            var span = loc.GetLineSpan();
            locView = new LocationView
            {
                FilePath = span.Path,
                StartLine = span.StartLinePosition.Line + 1,
                EndLine = span.EndLinePosition.Line + 1
            };
        }

        return new SymbolBriefView
        {
            SymbolId = sym.GetDocumentationCommentId() ?? sym.ToDisplayString(),
            Name = sym.Name,
            Signature = sym.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            Location = locView
        };
    }

    private static void AddCalleeIfNew(ISymbol? sym, HashSet<string> seen, List<SymbolBriefView> callees)
    {
        if (sym == null) return;
        var id = sym.GetDocumentationCommentId() ?? sym.ToDisplayString();
        if (seen.Add(id))
            callees.Add(ToBrief(sym));
    }

    private static async Task<string> GetSnippetAsync(
        Solution solution, ReferenceLocation refLoc, CancellationToken ct)
    {
        try
        {
            var doc = solution.GetDocument(refLoc.Location.SourceTree);
            if (doc == null) return "";
            var text = await doc.GetTextAsync(ct);
            var line = refLoc.Location.GetLineSpan().StartLinePosition.Line;
            return line >= 0 && line < text.Lines.Count
                ? text.Lines[line].ToString().Trim()
                : "";
        }
        catch
        {
            return "";
        }
    }

    private static List<SymbolBriefView> FindDerivedInIndex(string baseName, SolutionInfo info)
        => info.Projects
            .SelectMany(p => p.Namespaces).SelectMany(n => n.Types)
            .Where(t => t.BaseTypes.Any(bt => bt.Contains(baseName, StringComparison.OrdinalIgnoreCase)) ||
                        t.ImplementedInterfaces.Any(i => i.Contains(baseName, StringComparison.OrdinalIgnoreCase)))
            .Take(MaxDerivedTypes)
            .Select(t => new SymbolBriefView
            {
                SymbolId = GetId(t), Name = t.Name, Signature = t.Signature,
                Location = MapLocations(t.Locations).FirstOrDefault()
            }).ToList();

    private static List<SymbolBriefView> FindImplementationsInIndex(string ifaceName, SolutionInfo info)
        => info.Projects
            .SelectMany(p => p.Namespaces).SelectMany(n => n.Types)
            .Where(t => t.ImplementedInterfaces.Any(i => i.Contains(ifaceName, StringComparison.OrdinalIgnoreCase)))
            .Take(100)
            .Select(t => new SymbolBriefView
            {
                SymbolId = GetId(t), Name = t.Name, Signature = t.Signature,
                Location = MapLocations(t.Locations).FirstOrDefault()
            }).ToList();

    private SymbolSearchResult ToSearchResult(SymbolInfo sym, double score, SolutionInfo info)
    {
        var (containingType, containingNs) = ResolveContaining(sym, info);
        return new SymbolSearchResult
        {
            SymbolId = GetId(sym),
            Name = sym.Name,
            Kind = sym.Kind,
            Signature = sym.Signature,
            Accessibility = sym.Accessibility,
            ContainingType = containingType,
            ContainingNamespace = containingNs,
            Summary = sym is MemberInfo mi ? mi.Comment ?? sym.Summary : sym.Summary,
            Locations = MapLocations(sym.Locations),
            Score = Math.Round(score, 2)
        };
    }

    private static string GetId(SymbolInfo sym) => sym.UniqueId ?? sym.Signature;
}