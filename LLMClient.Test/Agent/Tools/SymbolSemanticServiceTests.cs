using AutoMapper;
using LLMClient.ContextEngineering.Analysis;
using LLMClient.ContextEngineering.Tools;
using Microsoft.Extensions.Logging;

namespace LLMClient.Test.Agent.Tools;

public class SymbolSemanticServiceTests
{
    private class TestSymbolIndex : SymbolSemanticService.ISymbolIndex
    {
        private readonly List<SymbolInfo> _allSymbols;

        public TestSymbolIndex(SolutionInfo solutionInfo)
        {
            _allSymbols = solutionInfo.Projects
                .SelectMany(p => p.Namespaces)
                .SelectMany(n => n.Types)
                .SelectMany(t => new SymbolInfo[] { t }.Concat(t.Members))
                .ToList();
        }

        public IEnumerable<(SymbolInfo Sym, double Score)> Search(string query, string? kind, string? scope)
        {
            return _allSymbols
                .Where(s => Matches(s, query, kind, scope, out _))
                .Select(s => (s, CalculateScore(s, query)));
        }

        public SymbolInfo? GetByKey(string key)
        {
            return _allSymbols.FirstOrDefault(s => s.SymbolId == key || s.UniqueId == key);
        }

        private bool Matches(SymbolInfo s, string query, string? kind, string? scope, out double score)
        {
            score = 0;
            if (!string.IsNullOrWhiteSpace(kind) && !string.Equals(s.Kind, kind, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(scope) && 
                !s.Locations.Any(l => l.FilePath != null && l.FilePath.Contains(scope, StringComparison.OrdinalIgnoreCase)) &&
                !s.Signature.Contains(scope))
                return false;

            return s.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || 
                   (s.Summary != null && s.Summary.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        private double CalculateScore(SymbolInfo s, string query)
        {
            if (string.Equals(s.Name, query, StringComparison.OrdinalIgnoreCase)) return 1.0;
            if (s.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 0.9;
            return 0.5;
        }
    }

    // ── 工厂 ──────────────────────────────────────────────────────────────

    private static SymbolSemanticService CreateService(SolutionInfo? solution = null)
    {
        var ctx = new SolutionContext(null!);
        if (solution != null)
            ctx.SetForTesting(solution);

        var config = new MapperConfiguration(cfg => cfg.AddProfile<RoslynMappingProfile>(),
            LoggerFactory.Create(builder => builder.AddDebug()));
        var mapper = config.CreateMapper();

        var service = new SymbolSemanticService(ctx, mapper);
        
        if (solution != null)
        {
            service.SetIndexServiceForTesting(new TestSymbolIndex(solution));
        }

        return service;
    }

    private static SymbolSemanticService CreateRichService()
        => CreateService(SymbolSemanticFixtures.BuildRichSolution());

    // ══════════════════════════════════════════════════════════════════════
    // SearchSymbols
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SearchSymbols_WhenNotLoaded_ThrowsInvalidOperation()
    {
        var svc = CreateService(); // 未注入 solution

        Assert.Throws<InvalidOperationException>(() => svc.SearchSymbols("User"));
    }

    [Fact]
    public void SearchSymbols_ExactNameMatch_ReturnsScoreOne()
    {
        var svc = CreateRichService();

        var results = svc.SearchSymbols("UserService");

        var top = Assert.Single(results.Where(r => r.Name == "UserService" && r.Kind == "Class"));
        Assert.Equal(1.0, top.Score);
    }

    [Fact]
    public void SearchSymbols_StartsWithMatch_ReturnsHigherScoreThanContains()
    {
        var svc = CreateRichService();

        // "UserService" starts-with "User"; "IUserService" contains "User"
        var results = svc.SearchSymbols("User", kind: "Class");

        var userService = results.First(r => r.Name == "UserService");
        var orderService = results.FirstOrDefault(r => r.Name == "OrderService");

        Assert.True(userService.Score >= 0.85,
            $"Expected score >= 0.85 for starts-with, got {userService.Score}");

        // OrderService 不含 "User"，应不出现
        Assert.Null(orderService);
    }

    [Fact]
    public void SearchSymbols_KindFilter_OnlyReturnsMatchingKind()
    {
        var svc = CreateRichService();

        var results = svc.SearchSymbols("Service", kind: "Interface");

        Assert.All(results, r => Assert.Equal("Interface", r.Kind));
        Assert.Single(results); // 只有 IUserService
    }

    [Fact]
    public void SearchSymbols_ScopeFilter_ByFilePathFragment()
    {
        var svc = CreateRichService();

        // scope 匹配 Controllers 目录下的文件
        var results = svc.SearchSymbols("Controller", scope: "Controllers");

        Assert.All(results, r =>
            Assert.Contains("Controller", r.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SearchSymbols_ScopeFilter_ExcludesOutOfScopeSymbols()
    {
        var svc = CreateRichService();

        // scope 限制到 MyApp.Core.Models 命名空间
        var results = svc.SearchSymbols("User", scope: "MyApp.Core.Models");

        // UserService 在 Services 命名空间，不应出现
        Assert.DoesNotContain(results, r => r.Name == "UserService");
    }

    [Fact]
    public void SearchSymbols_TopK_LimitsResultCount()
    {
        var svc = CreateRichService();

        var results = svc.SearchSymbols("a", topK: 2); // "a" 会匹配多个

        Assert.True(results.Count <= 2);
    }

    [Fact]
    public void SearchSymbols_NoMatch_ReturnsEmptyList()
    {
        var svc = CreateRichService();

        var results = svc.SearchSymbols("XyzDoesNotExist");

        Assert.Empty(results);
    }

    [Fact]
    public void SearchSymbols_ReturnsContainingTypeAndNamespace()
    {
        var svc = CreateRichService();

        // SaveAsync 是 UserService 的成员
        var results = svc.SearchSymbols("SaveAsync");

        var result = Assert.Single(results);
        Assert.Equal("UserService", result.ContainingType);
        Assert.Equal("MyApp.Core.Services", result.ContainingNamespace);
    }

    [Fact]
    public void SearchSymbols_MatchesXmlSummary()
    {
        var svc = CreateRichService();

        // "entity" 在 SaveAsync 的 Comment 中，不在 Name/Signature 中
        var results = svc.SearchSymbols("entity asynchronously");

        Assert.Contains(results, r => r.Name == "SaveAsync");
    }

    // ══════════════════════════════════════════════════════════════════════
    // GetSymbolDetail
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetSymbolDetail_TypeSymbol_PopulatesTypeDetail()
    {
        var svc = CreateRichService();

        var detail = svc.GetSymbolDetail(SymbolSemanticFixtures.UserServiceId);

        Assert.Equal("UserService", detail.Name);
        Assert.Equal("Class", detail.Kind);
        Assert.NotNull(detail.TypeDetail);
        Assert.Null(detail.MemberDetail);
        Assert.Contains("IUserService", detail.TypeDetail.ImplementedInterfaces);
        Assert.Equal(4, detail.TypeDetail.MemberCount); // SaveAsync, GetById, Name, _repository
    }

    [Fact]
    public void GetSymbolDetail_MethodSymbol_PopulatesMemberDetail()
    {
        var svc = CreateRichService();

        var detail = svc.GetSymbolDetail(SymbolSemanticFixtures.SaveAsyncId);

        Assert.Equal("SaveAsync", detail.Name);
        Assert.Equal("Method", detail.Kind);
        Assert.NotNull(detail.MemberDetail);
        Assert.Null(detail.TypeDetail);
        Assert.True(detail.MemberDetail.IsAsync);
        Assert.True(detail.MemberDetail.IsVirtual);
        Assert.Equal("Task<bool>", detail.MemberDetail.ReturnType);
        Assert.Single(detail.MemberDetail.Parameters!);
        Assert.Equal("user", detail.MemberDetail.Parameters![0].Name);
    }

    [Fact]
    public void GetSymbolDetail_ByPlainName_ResolvesWhenUnambiguous()
    {
        var svc = CreateRichService();

        // "OrderService" 是唯一匹配的名称
        var detail = svc.GetSymbolDetail(SymbolSemanticFixtures.OrderServiceId);

        Assert.Equal("OrderService", detail.Name);
    }

    [Fact]
    public void GetSymbolDetail_UnknownId_ThrowsArgumentException()
    {
        var svc = CreateRichService();

        Assert.Throws<ArgumentException>(() => svc.GetSymbolDetail("T:Does.Not.Exist"));
    }

    [Fact]
    public void GetSymbolDetail_IncludesLocations()
    {
        var svc = CreateRichService();

        var detail = svc.GetSymbolDetail(SymbolSemanticFixtures.UserServiceId);

        Assert.NotEmpty(detail.Locations);
        Assert.All(detail.Locations, l => Assert.False(string.IsNullOrWhiteSpace(l.FilePath)));
    }

    [Fact]
    public void GetSymbolDetail_Summary_PropagatedFromXmlComment()
    {
        var svc = CreateRichService();

        var detail = svc.GetSymbolDetail(SymbolSemanticFixtures.SaveAsyncId);

        Assert.Equal("Saves a user entity asynchronously.", detail.Summary);
    }

    // ══════════════════════════════════════════════════════════════════════
    // GetTypeMembers
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetTypeMembers_NoFilter_ReturnsAllMembers()
    {
        var svc = CreateRichService();

        var view = svc.GetTypeMembers(SymbolSemanticFixtures.UserServiceId);

        Assert.Equal(4, view.TotalCount);
        Assert.Equal("UserService", view.Name);
    }

    [Fact]
    public void GetTypeMembers_KindFilter_Method_ReturnsTwoMethods()
    {
        var svc = CreateRichService();

        var view = svc.GetTypeMembers(SymbolSemanticFixtures.UserServiceId, kindFilter: "Method");

        Assert.Equal(2, view.TotalCount);
        Assert.All(view.Members, m => Assert.Equal("Method", m.Kind));
    }

    [Fact]
    public void GetTypeMembers_KindFilter_Property_ReturnsSingleProperty()
    {
        var svc = CreateRichService();

        var view = svc.GetTypeMembers(SymbolSemanticFixtures.UserServiceId, kindFilter: "Property");

        Assert.Equal(1, view.TotalCount);
        Assert.Equal("Name", view.Members[0].Name);
    }

    [Fact]
    public void GetTypeMembers_AccessibilityFilter_Public_ExcludesPrivate()
    {
        var svc = CreateRichService();

        var view = svc.GetTypeMembers(
            SymbolSemanticFixtures.UserServiceId,
            accessibilityFilter: "Public");

        Assert.DoesNotContain(view.Members, m => m.Name == "_repository");
    }

    [Fact]
    public void GetTypeMembers_MemberContainsLocation()
    {
        var svc = CreateRichService();

        var view = svc.GetTypeMembers(SymbolSemanticFixtures.UserServiceId, kindFilter: "Method");

        Assert.All(view.Members, m => Assert.NotNull(m.Location));
    }

    [Fact]
    public void GetTypeMembers_AsyncMethod_IsAsyncFlagSet()
    {
        var svc = CreateRichService();

        var view = svc.GetTypeMembers(SymbolSemanticFixtures.UserServiceId, kindFilter: "Method");

        var saveAsync = view.Members.Single(m => m.Name == "SaveAsync");
        Assert.True(saveAsync.IsAsync);

        var getById = view.Members.Single(m => m.Name == "GetById");
        Assert.False(getById.IsAsync);
    }

    [Fact]
    public void GetTypeMembers_UnknownTypeId_ThrowsArgumentException()
    {
        var svc = CreateRichService();

        Assert.Throws<ArgumentException>(() =>
            svc.GetTypeMembers("T:Does.Not.Exist.Type"));
    }

    // ══════════════════════════════════════════════════════════════════════
    // GetTypeHierarchyAsync — Roslyn null → 静态索引兜底
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetTypeHierarchyAsync_IndexFallback_ReturnsSourceIndex()
    {
        var svc = CreateRichService();

        var view = await svc.GetTypeHierarchyAsync(SymbolSemanticFixtures.UserServiceId);

        Assert.Equal("Index", view.Source);
    }

    [Fact]
    public async Task GetTypeHierarchyAsync_PopulatesInterfacesFromIndex()
    {
        var svc = CreateRichService();

        var view = await svc.GetTypeHierarchyAsync(SymbolSemanticFixtures.UserServiceId);

        Assert.Contains("IUserService", view.ImplementedInterfaces);
    }

    [Fact]
    public async Task GetTypeHierarchyAsync_InterfaceType_FindsImplementorsInIndex()
    {
        var svc = CreateRichService();

        // IUserService 的实现者：UserService（ImplementedInterfaces 含 "IUserService"）
        var view = await svc.GetTypeHierarchyAsync(SymbolSemanticFixtures.IUserServiceId);

        Assert.Contains(view.DerivedTypes, d => d.Name == "UserService");
    }

    [Fact]
    public async Task GetTypeHierarchyAsync_UnknownId_ThrowsArgumentException()
    {
        var svc = CreateRichService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.GetTypeHierarchyAsync("T:No.Such.Type"));
    }

    // ══════════════════════════════════════════════════════════════════════
    // GetInterfaceImplementationsAsync — 静态索引兜底
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetInterfaceImplementationsAsync_FindsImplementorFromIndex()
    {
        var svc = CreateRichService();

        var view = await svc.GetInterfaceImplementationsAsync(SymbolSemanticFixtures.IUserServiceId);

        Assert.Equal("Index", view.Source);
        Assert.Contains(view.Implementations, i => i.Name == "UserService");
    }

    [Fact]
    public async Task GetInterfaceImplementationsAsync_InterfaceWithNoImplementors_ReturnsEmpty()
    {
        var svc = CreateRichService();
        var solution = SymbolSemanticFixtures.BuildRichSolution();
        var loneInterface = new TypeInfo
        {
            UniqueId = "T:MyApp.Core.Services.IAlone",
            Name = "IAlone",
            Kind = "Interface",
            Signature = "public interface IAlone",
            Accessibility = "Public",
            ImplementedInterfaces = [],
            Attributes = [],
            Locations =
                [SymbolSemanticFixtures.Loc(@"C:\Projects\MyApp\MyApp.Core\Services\IAlone.cs", 1, 40)]
        };
        solution.Projects.First().Namespaces.First().Types.Add(loneInterface);

        var ctx = new SolutionContext(null!);
        ctx.SetForTesting(solution);
        var config = new MapperConfiguration(cfg => cfg.AddProfile<RoslynMappingProfile>(),
            LoggerFactory.Create(builder => builder.AddDebug()));
        var mapper = config.CreateMapper();
        
        var isolatedSvc = new SymbolSemanticService(ctx, mapper);
        isolatedSvc.SetIndexServiceForTesting(new TestSymbolIndex(solution));

        var view = await isolatedSvc.GetInterfaceImplementationsAsync("T:MyApp.Core.Services.IAlone");
        Assert.Empty(view.Implementations);
    }

    // ══════════════════════════════════════════════════════════════════════
    // GetCallersAsync / GetCalleesAsync / GetUsagesAsync — Roslyn 必需
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetCallersAsync_WithoutRoslyn_ThrowsInvalidOperation()
    {
        // SolutionContext.SetForTesting 将 RoslynSolution 置为 null，
        // RequireRoslynSolution 会抛出，在 Service 层不被吞掉
        var svc = CreateRichService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GetCallersAsync(SymbolSemanticFixtures.SaveAsyncId));
    }

    [Fact]
    public async Task GetCalleesAsync_WithoutRoslyn_ThrowsInvalidOperation()
    {
        var svc = CreateRichService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GetCalleesAsync(SymbolSemanticFixtures.SaveAsyncId));
    }

    [Fact]
    public async Task GetUsagesAsync_WithoutRoslyn_ThrowsInvalidOperation()
    {
        var svc = CreateRichService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GetUsagesAsync(SymbolSemanticFixtures.UserServiceId));
    }

    [Fact]
    public async Task GetCallersAsync_UnknownSymbolId_ThrowsArgumentException()
    {
        var svc = CreateRichService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.GetCallersAsync("M:Does.Not.Exist.Method"));
    }

    // ══════════════════════════════════════════════════════════════════════
    // GetDependencyGraph
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetDependencyGraph_AllProjects_ContainsAllProjectNodes()
    {
        var svc = CreateRichService();

        var graph = svc.GetDependencyGraph();

        var projectNodes = graph.Nodes.Where(n => n.Kind == "Project").ToList();
        Assert.Equal(2, projectNodes.Count);
        Assert.Contains(projectNodes, n => n.Name == "MyApp.Core");
        Assert.Contains(projectNodes, n => n.Name == "MyApp.Api");
    }

    [Fact]
    public void GetDependencyGraph_ApiProject_HasEdgeToCore()
    {
        var svc = CreateRichService();

        var graph = svc.GetDependencyGraph(projectName: "MyApp.Api", depth: 2);

        var edge = graph.Edges.FirstOrDefault(e =>
            graph.Nodes.Any(n => n.Id == e.To && n.Name == "MyApp.Core"));

        Assert.NotNull(edge);
        Assert.Equal("ProjectReference", edge.Kind);
    }

    [Fact]
    public void GetDependencyGraph_SingleProject_OnlyThatRootAndDeps()
    {
        var svc = CreateRichService();

        var graph = svc.GetDependencyGraph(projectName: "MyApp.Core", depth: 1);

        // Core 没有项目引用，只有自身节点和包引用节点
        Assert.Contains(graph.Nodes, n => n.Name == "MyApp.Core");
        Assert.DoesNotContain(graph.Nodes, n => n.Name == "MyApp.Api");
    }

    [Fact]
    public void GetDependencyGraph_RootNode_IncludesPackageReferences()
    {
        var svc = CreateRichService();

        var graph = svc.GetDependencyGraph(projectName: "MyApp.Core", depth: 1);

        var pkgNodes = graph.Nodes.Where(n => n.Kind == "Package").ToList();
        // TestFixtures.BuildCoreProject 有 2 个包
        Assert.Equal(2, pkgNodes.Count);
        Assert.Contains(pkgNodes, n => n.Name.Contains("Newtonsoft.Json"));
    }

    [Fact]
    public void GetDependencyGraph_UnknownProject_ThrowsArgumentException()
    {
        var svc = CreateRichService();

        Assert.Throws<ArgumentException>(() => svc.GetDependencyGraph("NoSuchProject"));
    }

    // ══════════════════════════════════════════════════════════════════════
    // GetNamespaceTypes
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetNamespaceTypes_ExactNamespace_ReturnsOnlyDirectTypes()
    {
        var svc = CreateRichService();

        var view = svc.GetNamespaceTypes("MyApp.Core.Services", includeSubNamespaces: false);

        Assert.Equal(3, view.TotalCount); // UserService, IUserService, OrderService
        Assert.False(view.IncludesSubNamespaces);
    }

    [Fact]
    public void GetNamespaceTypes_WithSubNamespaces_IncludesChildNamespaces()
    {
        var svc = CreateRichService();

        // "MyApp.Core" 的子命名空间包含 MyApp.Core.Services 和 MyApp.Core.Models
        var view = svc.GetNamespaceTypes("MyApp.Core", includeSubNamespaces: true);

        Assert.True(view.TotalCount >= 4); // 3 services + 1 model
        Assert.True(view.IncludesSubNamespaces);
    }

    [Fact]
    public void GetNamespaceTypes_NonExistentNamespace_ReturnsEmpty()
    {
        var svc = CreateRichService();

        var view = svc.GetNamespaceTypes("MyApp.DoesNotExist");

        Assert.Equal(0, view.TotalCount);
        Assert.Empty(view.Types);
    }

    [Fact]
    public void GetNamespaceTypes_TypeSummary_ContainsExpectedFields()
    {
        var svc = CreateRichService();

        var view = svc.GetNamespaceTypes("MyApp.Core.Services");

        var userType = view.Types.Single(t => t.Name == "UserService");
        Assert.False(string.IsNullOrWhiteSpace(userType.SymbolId));
        Assert.Equal("Class", userType.Kind);
        Assert.Equal(4, userType.MemberCount);
        Assert.NotNull(userType.Location);
    }

    [Fact]
    public void GetNamespaceTypes_ResultsOrderedByKindThenName()
    {
        var svc = CreateRichService();

        var view = svc.GetNamespaceTypes("MyApp.Core.Services");

        // Interface 字母序在 Class 之前
        var kinds = view.Types.Select(t => t.Kind).ToList();
        var sorted = view.Types.OrderBy(t => t.Kind).ThenBy(t => t.Name).Select(t => t.Kind).ToList();
        Assert.Equal(sorted, kinds);
    }
}