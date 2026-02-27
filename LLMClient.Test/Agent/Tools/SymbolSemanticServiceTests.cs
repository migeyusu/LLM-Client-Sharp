using LLMClient.ContextEngineering;
using LLMClient.ContextEngineering.Analysis;
using LLMClient.ContextEngineering.Tools;
using Xunit;

namespace LLMClient.Test.Agent.Tools;

public sealed class SymbolSemanticServiceTests
{
    // ── 共享基础设施 ──────────────────────────────────────────────────

    private static SymbolSemanticService CreateService(out SolutionContext ctx)
    {
        ctx = new SolutionContext();
        ctx.SetForTesting(SymbolSemanticTestFixtures.BuildRichSolution());
        return new SymbolSemanticService(ctx);
    }

    private SymbolSemanticService _svc;
    private SolutionContext _ctx;

    public SymbolSemanticServiceTests()
    {
        
        _svc = CreateService(out _ctx);
    }

    // ════════════════════════════════════════════════════════════════════
    // SearchSymbols
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void SearchSymbols_ExactNameMatch_ReturnsTopScore()
    {
        var results = _svc.SearchSymbols("UserService");

        Assert.NotEmpty(results);
        var top = results.First();
        Assert.Equal("UserService", top.Name);
        Assert.Equal(1.0, top.Score);
    }

    [Fact]
    public void SearchSymbols_PrefixMatch_ReturnsExpectedScore()
    {
        var results = _svc.SearchSymbols("User");

        // "UserService" 和 "UserController" 都以 User 开头
        Assert.True(results.All(r => r.Score >= 0.85));
        Assert.Contains(results, r => r.Name == "UserService");
    }

    [Fact]
    public void SearchSymbols_SubstringMatch_ReturnsResults()
    {
        var results = _svc.SearchSymbols("Service");

        Assert.Contains(results, r => r.Name == "UserService");
        Assert.Contains(results, r => r.Name == "OrderService");
    }

    [Fact]
    public void SearchSymbols_KindFilter_Class_ExcludesInterfaces()
    {
        var results = _svc.SearchSymbols("Service", kind: "Class");

        Assert.All(results, r => Assert.Equal("Class", r.Kind));
        Assert.DoesNotContain(results, r => r.Kind == "Interface");
    }

    [Fact]
    public void SearchSymbols_KindFilter_Interface_ReturnsOnlyInterfaces()
    {
        var results = _svc.SearchSymbols("IUserService", kind: "Interface");

        Assert.Single(results);
        Assert.Equal("Interface", results[0].Kind);
        Assert.Equal("IUserService", results[0].Name);
    }

    [Fact]
    public void SearchSymbols_KindFilter_Method_ReturnsMethods()
    {
        var results = _svc.SearchSymbols("SaveAsync", kind: "Method");

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal("Method", r.Kind));
    }

    [Fact]
    public void SearchSymbols_ScopeFilter_PathFragment_NarrowsResults()
    {
        var results = _svc.SearchSymbols("Service", scope: "Services");

        // 不应包含 Controller（在 Controllers 路径下）
        Assert.DoesNotContain(results, r => r.Name == "UserController");
    }

    [Fact]
    public void SearchSymbols_ScopeFilter_NamespacePrefix_NarrowsResults()
    {
        var results = _svc.SearchSymbols("User", scope: "MyApp.Core");

        Assert.Contains(results, r => r.Name == "UserService");
        Assert.DoesNotContain(results, r => r.Name == "UserController");
    }

    [Fact]
    public void SearchSymbols_NoMatch_ReturnsEmpty()
    {
        var results = _svc.SearchSymbols("ZzzNonExistent");

        Assert.Empty(results);
    }

    [Fact]
    public void SearchSymbols_TopK_LimitsResultCount()
    {
        var results = _svc.SearchSymbols("a", topK: 2);

        Assert.True(results.Count <= 2);
    }

    [Fact]
    public void SearchSymbols_ResultsContainContainingTypeForMembers()
    {
        var results = _svc.SearchSymbols("SaveAsync", kind: "Method");

        var userServiceMember = results.FirstOrDefault(r =>
            r.SymbolId == SymbolSemanticTestFixtures.SaveAsyncMemberId);
        Assert.NotNull(userServiceMember);
        Assert.Equal("UserService", userServiceMember.ContainingType);
        Assert.Equal("MyApp.Core.Services", userServiceMember.ContainingNamespace);
    }

    [Fact]
    public void SearchSymbols_ResultsAreSortedByScoreDescending()
    {
        var results = _svc.SearchSymbols("User");

        var scores = results.Select(r => r.Score).ToList();
        Assert.Equal(scores.OrderByDescending(s => s).ToList(), scores);
    }

    [Fact]
    public void SearchSymbols_SignatureMatch_ReturnsResultsWithLowerScore()
    {
        // "Task<bool>" 只出现在 Signature 中，不在 Name 中
        var results = _svc.SearchSymbols("Task<bool>");

        Assert.All(results, r => Assert.True(r.Score <= 0.45));
    }

    // ════════════════════════════════════════════════════════════════════
    // GetSymbolDetail
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetSymbolDetail_TypeById_ReturnsTypeDetail()
    {
        var detail = _svc.GetSymbolDetail(SymbolSemanticTestFixtures.UserServiceTypeId);

        Assert.Equal("UserService", detail.Name);
        Assert.Equal("Class", detail.Kind);
        Assert.NotNull(detail.TypeDetail);
        Assert.Null(detail.MemberDetail);
        Assert.Contains("IUserService", detail.TypeDetail.ImplementedInterfaces);
        Assert.Equal(4, detail.TypeDetail.MemberCount); // SaveAsync, GetById, Delete, ValidateInternal
    }

    [Fact]
    public void GetSymbolDetail_TypeById_IncludesLocations()
    {
        var detail = _svc.GetSymbolDetail(SymbolSemanticTestFixtures.UserServiceTypeId);

        Assert.NotEmpty(detail.Locations);
        Assert.Contains(detail.Locations, l => l.FilePath.EndsWith("UserService.cs"));
    }

    [Fact]
    public void GetSymbolDetail_TypeById_IncludesSummary()
    {
        var detail = _svc.GetSymbolDetail(SymbolSemanticTestFixtures.UserServiceTypeId);

        Assert.Equal("Handles user business logic.", detail.Summary);
    }

    [Fact]
    public void GetSymbolDetail_MemberById_ReturnsMemberDetail()
    {
        var detail = _svc.GetSymbolDetail(SymbolSemanticTestFixtures.SaveAsyncMemberId);

        Assert.Equal("SaveAsync", detail.Name);
        Assert.Equal("Method", detail.Kind);
        Assert.NotNull(detail.MemberDetail);
        Assert.Null(detail.TypeDetail);
        Assert.Equal("Task<bool>", detail.MemberDetail.ReturnType);
        Assert.True(detail.MemberDetail.IsAsync);
    }

    [Fact]
    public void GetSymbolDetail_MemberById_IncludesParameters()
    {
        var detail = _svc.GetSymbolDetail(SymbolSemanticTestFixtures.SaveAsyncMemberId);

        Assert.NotNull(detail.MemberDetail!.Parameters);
        var param = Assert.Single(detail.MemberDetail.Parameters!);
        Assert.Equal("user", param.Name);
        Assert.Equal("User", param.Type);
    }

    [Fact]
    public void GetSymbolDetail_MemberById_IncludesContainingType()
    {
        var detail = _svc.GetSymbolDetail(SymbolSemanticTestFixtures.SaveAsyncMemberId);

        Assert.Equal("UserService", detail.MemberDetail!.ContainingType);
    }

    [Fact]
    public void GetSymbolDetail_ByPlainName_FallbackToNameMatch()
    {
        // 使用纯名称（无 UniqueId 格式）也应能命中
        var detail = _svc.GetSymbolDetail("UserService");

        Assert.Equal("UserService", detail.Name);
    }

    [Fact]
    public void GetSymbolDetail_NotFound_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _svc.GetSymbolDetail("T:Completely.Nonexistent.Type"));
    }

    [Fact]
    public void GetSymbolDetail_InterfaceType_ReturnsTypeDetail()
    {
        var detail = _svc.GetSymbolDetail(SymbolSemanticTestFixtures.IUserServiceTypeId);

        Assert.Equal("IUserService", detail.Name);
        Assert.Equal("Interface", detail.Kind);
        Assert.NotNull(detail.TypeDetail);
    }

    // ════════════════════════════════════════════════════════════════════
    // GetTypeMembers
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetTypeMembers_NoFilter_ReturnsAllMembers()
    {
        var view = _svc.GetTypeMembers(SymbolSemanticTestFixtures.UserServiceTypeId);

        Assert.Equal("UserService", view.TypeName);
        Assert.Equal(4, view.TotalCount);
        Assert.Equal(4, view.Members.Count);
    }

    [Fact]
    public void GetTypeMembers_KindFilter_Method_ReturnsOnlyMethods()
    {
        var view = _svc.GetTypeMembers(
            SymbolSemanticTestFixtures.UserServiceTypeId, kindFilter: "Method");

        Assert.All(view.Members, m => Assert.Equal("Method", m.Kind));
    }

    [Fact]
    public void GetTypeMembers_AccessibilityFilter_Public_ExcludesPrivate()
    {
        var view = _svc.GetTypeMembers(
            SymbolSemanticTestFixtures.UserServiceTypeId,
            accessibilityFilter: "Public");

        Assert.DoesNotContain(view.Members, m => m.Name == "ValidateInternal");
        Assert.Contains(view.Members, m => m.Name == "SaveAsync");
    }

    [Fact]
    public void GetTypeMembers_EachMemberHasSymbolId()
    {
        var view = _svc.GetTypeMembers(SymbolSemanticTestFixtures.UserServiceTypeId);

        Assert.All(view.Members, m => Assert.False(string.IsNullOrWhiteSpace(m.SymbolId)));
    }

    [Fact]
    public void GetTypeMembers_EachMemberHasLocation()
    {
        var view = _svc.GetTypeMembers(SymbolSemanticTestFixtures.UserServiceTypeId);

        Assert.All(view.Members, m => Assert.NotNull(m.Location));
    }

    [Fact]
    public void GetTypeMembers_AsyncMembersMarkedCorrectly()
    {
        var view = _svc.GetTypeMembers(SymbolSemanticTestFixtures.UserServiceTypeId);

        var saveAsync = view.Members.Single(m => m.Name == "SaveAsync");
        Assert.True(saveAsync.IsAsync);

        var delete = view.Members.Single(m => m.Name == "Delete");
        Assert.False(delete.IsAsync);
    }

    [Fact]
    public void GetTypeMembers_ByPlainName_ResolvesCorrectly()
    {
        var view = _svc.GetTypeMembers("UserService");

        Assert.Equal("UserService", view.TypeName);
    }

    [Fact]
    public void GetTypeMembers_NotFound_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _svc.GetTypeMembers("T:Completely.Nonexistent"));
    }

    [Fact]
    public void GetTypeMembers_PropertyType_ReturnedCorrectly()
    {
        var view = _svc.GetTypeMembers(SymbolSemanticTestFixtures.UserTypeId);

        var idProp = view.Members.Single(m => m.Name == "Id");
        Assert.Equal("int", idProp.ReturnType);
        Assert.Equal("Property", idProp.Kind);
    }

    // ════════════════════════════════════════════════════════════════════
    // GetTypeHierarchyAsync — 无 Roslyn 时走索引兜底
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetTypeHierarchyAsync_NoRoslyn_FallsBackToIndex()
    {
        // SolutionContext 仅有静态分析数据，Roslyn 不可用，应走 Source="Index"
        var view = await _svc.GetTypeHierarchyAsync(
            SymbolSemanticTestFixtures.UserServiceTypeId);

        Assert.Equal("UserService", view.TypeName);
        Assert.Equal("Index", view.Source);
        Assert.Contains("IUserService", view.ImplementedInterfaces);
    }

    [Fact]
    public async Task GetTypeHierarchyAsync_Interface_FindsImplementingClasses()
    {
        // IUserService 被 UserService 实现，应在 DerivedTypes 中出现
        var view = await _svc.GetTypeHierarchyAsync(
            SymbolSemanticTestFixtures.IUserServiceTypeId);

        Assert.Contains(view.DerivedTypes, d => d.Name == "UserService");
        Assert.Equal("Index", view.Source);
    }

    [Fact]
    public async Task GetTypeHierarchyAsync_NotFound_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _svc.GetTypeHierarchyAsync("T:Nonexistent"));
    }

    // ════════════════════════════════════════════════════════════════════
    // GetInterfaceImplementationsAsync — 无 Roslyn 时走索引兜底
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetInterfaceImplementationsAsync_NoRoslyn_FallsBackToIndex()
    {
        var view = await _svc.GetInterfaceImplementationsAsync(
            SymbolSemanticTestFixtures.IUserServiceTypeId);

        Assert.Equal("IUserService", view.InterfaceName);
        Assert.Equal("Index", view.Source);
        Assert.Contains(view.Implementations, i => i.Name == "UserService");
    }

    [Fact]
    public async Task GetInterfaceImplementationsAsync_NotFound_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _svc.GetInterfaceImplementationsAsync("T:Nonexistent"));
    }

    // ════════════════════════════════════════════════════════════════════
    // GetCallersAsync / GetCalleesAsync / GetUsagesAsync
    // 无 Roslyn Solution 时抛出 InvalidOperationException
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetCallersAsync_NoRoslyn_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _svc.GetCallersAsync(SymbolSemanticTestFixtures.SaveAsyncMemberId));
    }

    [Fact]
    public async Task GetCalleesAsync_NoRoslyn_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _svc.GetCalleesAsync(SymbolSemanticTestFixtures.SaveAsyncMemberId));
    }

    [Fact]
    public async Task GetUsagesAsync_NoRoslyn_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _svc.GetUsagesAsync(SymbolSemanticTestFixtures.SaveAsyncMemberId));
    }

    [Fact]
    public async Task GetCallersAsync_SymbolNotFound_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _svc.GetCallersAsync("T:Nonexistent"));
    }

    // ════════════════════════════════════════════════════════════════════
    // GetDependencyGraph
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetDependencyGraph_AllProjects_ContainsAllNodes()
    {
        var graph = _svc.GetDependencyGraph();

        var projectNodeNames = graph.Nodes
            .Where(n => n.Kind == "Project")
            .Select(n => n.Name)
            .ToList();

        Assert.Contains("MyApp.Core", projectNodeNames);
        Assert.Contains("MyApp.Api", projectNodeNames);
    }

    [Fact]
    public void GetDependencyGraph_AllProjects_ContainsProjectReferenceEdge()
    {
        var graph = _svc.GetDependencyGraph();

        // MyApp.Api → MyApp.Core
        Assert.Contains(graph.Edges, e =>
            e.Kind == "ProjectReference" &&
            graph.Nodes.Any(n => n.Id == e.From && n.Name == "MyApp.Api") &&
            graph.Nodes.Any(n => n.Id == e.To && n.Name == "MyApp.Core"));
    }

    [Fact]
    public void GetDependencyGraph_AllProjects_ContainsPackageNodes()
    {
        var graph = _svc.GetDependencyGraph();

        Assert.Contains(graph.Nodes, n => n.Kind == "Package" &&
                                          n.Name.Contains("Newtonsoft.Json"));
    }

    [Fact]
    public void GetDependencyGraph_SingleProject_OnlyIncludesReachableNodes()
    {
        var graph = _svc.GetDependencyGraph("MyApp.Core");

        Assert.DoesNotContain(graph.Nodes, n =>
            n.Kind == "Project" && n.Name == "MyApp.Api");
    }

    [Fact]
    public void GetDependencyGraph_ScopeLabel_MatchesProjectName()
    {
        var graph = _svc.GetDependencyGraph("MyApp.Core");

        Assert.Equal("MyApp.Core", graph.ScopeLabel);
    }

    [Fact]
    public void GetDependencyGraph_ScopeLabel_UsesSolutionNameWhenNoProject()
    {
        var graph = _svc.GetDependencyGraph();

        Assert.Equal("MyApp", graph.ScopeLabel);
    }

    [Fact]
    public void GetDependencyGraph_InvalidProjectName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _svc.GetDependencyGraph("Nonexistent.Project"));
    }

    [Fact]
    public void GetDependencyGraph_DepthClamped_DoesNotExceedMaximum()
    {
        // depth=10 应被 clamp 到 5，不应抛出
        var ex = Record.Exception(() => _svc.GetDependencyGraph(depth: 10));
        Assert.Null(ex);
    }

    // ════════════════════════════════════════════════════════════════════
    // GetNamespaceTypes
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetNamespaceTypes_ExactMatch_ReturnsDirectTypes()
    {
        var view = _svc.GetNamespaceTypes("MyApp.Core.Models", includeSubNamespaces: false);

        Assert.Equal("MyApp.Core.Models", view.Namespace);
        Assert.Contains(view.Types, t => t.Name == "User");
        Assert.DoesNotContain(view.Types, t => t.Name == "UserService");
    }

    [Fact]
    public void GetNamespaceTypes_IncludeSubNamespaces_ReturnsAllDescendants()
    {
        var view = _svc.GetNamespaceTypes("MyApp.Core", includeSubNamespaces: true);

        // MyApp.Core.Services, MyApp.Core.Models 都应包含
        Assert.Contains(view.Types, t => t.Name == "UserService");
        Assert.Contains(view.Types, t => t.Name == "User");
    }

    [Fact]
    public void GetNamespaceTypes_TypesHaveSymbolId()
    {
        var view = _svc.GetNamespaceTypes("MyApp.Core.Models");

        Assert.All(view.Types, t => Assert.False(string.IsNullOrWhiteSpace(t.SymbolId)));
    }

    [Fact]
    public void GetNamespaceTypes_TypesHaveMemberCount()
    {
        var view = _svc.GetNamespaceTypes("MyApp.Core.Services");

        var userServiceView = view.Types.Single(t => t.Name == "UserService");
        Assert.Equal(4, userServiceView.MemberCount);
    }

    [Fact]
    public void GetNamespaceTypes_EmptyNamespace_ReturnsEmptyList()
    {
        var view = _svc.GetNamespaceTypes("MyApp.Nonexistent.Namespace");

        Assert.Equal(0, view.TotalCount);
        Assert.Empty(view.Types);
    }

    [Fact]
    public void GetNamespaceTypes_SortedByKindThenName()
    {
        var view = _svc.GetNamespaceTypes("MyApp.Core.Services");

        // Interface 在 Class 之前（字母序 C > I 不对，应看 Kind 排序）
        var kinds = view.Types.Select(t => t.Kind).ToList();
        var expectedOrder = kinds.OrderBy(k => k).ToList();
        Assert.Equal(expectedOrder, kinds);
    }

    [Fact]
    public void GetNamespaceTypes_TotalCount_MatchesMemberListCount()
    {
        var view = _svc.GetNamespaceTypes("MyApp.Core.Services", includeSubNamespaces: false);

        Assert.Equal(view.TotalCount, view.Types.Count);
    }

    // ════════════════════════════════════════════════════════════════════
    // SolutionContext 未加载时
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void SearchSymbols_NoSolution_ThrowsInvalidOperationException()
    {
        var emptyCtx = new SolutionContext();
        var svc = new SymbolSemanticService(emptyCtx);

        Assert.Throws<InvalidOperationException>(() => svc.SearchSymbols("User"));
    }
}