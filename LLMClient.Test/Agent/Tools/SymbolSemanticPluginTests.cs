using System.Text.Json.Nodes;
using AutoMapper;
using LLMClient.ContextEngineering.Analysis;
using LLMClient.ContextEngineering.Tools;
using Microsoft.Extensions.Logging;

namespace LLMClient.Test.Agent.Tools;

/// <summary>
/// Plugin 层测试的关注点：
/// 1. 所有路径均返回合法 JSON（不抛出异常）
/// 2. 错误场景返回 { "error": "..." } 结构
/// 3. 成功场景返回含预期字段的 JSON 对象
/// </summary>
public class SymbolSemanticPluginTests
{
    private class TestSymbolIndex : SymbolSemanticService.ISymbolIndex
    {
        private readonly List<SymbolInfo> _allSymbols;
        public TestSymbolIndex(SolutionInfo solutionInfo) {
            _allSymbols = solutionInfo.Projects.SelectMany(p => p.Namespaces)
                .SelectMany(n => n.Types).SelectMany(t => new SymbolInfo[] { t }.Concat(t.Members)).ToList();
        }
        public IEnumerable<(SymbolInfo Sym, double Score)> Search(string query, string? kind, string? scope) {
            return _allSymbols.Where(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(s => (s, s.Name.Equals(query, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.5));
        }
        public SymbolInfo? GetByKey(string key) => _allSymbols.FirstOrDefault(s => s.SymbolId == key || s.UniqueId == key);
    }

    // ── 工厂 ──────────────────────────────────────────────────────────────

    private static SymbolSemanticPlugin CreatePlugin(SolutionInfo? solution = null)
    {
        var ctx = new SolutionContext(null!);
        if (solution != null)
            ctx.SetForTesting(solution);

        var config = new MapperConfiguration(cfg => cfg.AddProfile<RoslynMappingProfile>(),
            LoggerFactory.Create(builder => builder.AddDebug()));
        var mapper = config.CreateMapper();

        var svc = new SymbolSemanticService(ctx, mapper);
        
        if (solution != null)
        {
            svc.SetIndexServiceForTesting(new TestSymbolIndex(solution));
        }
        
        return new SymbolSemanticPlugin(svc);
    }

    private static SymbolSemanticPlugin CreateRichPlugin()
        => CreatePlugin(SymbolSemanticFixtures.BuildRichSolution());

    // ── JSON 工具 ─────────────────────────────────────────────────────────

    private static JsonNode ParseJson(string json)
    {
        var node = JsonNode.Parse(json);
        Assert.NotNull(node);
        return node!;
    }

    private static bool IsErrorJson(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            return node?["error"] != null;
        }
        catch { return false; }
    }

    private static bool IsArrayJson(string json) =>
        json.TrimStart().StartsWith('[');

    // ══════════════════════════════════════════════════════════════════════
    // 通用守卫：未加载 solution 时所有工具返回 error JSON
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SearchSymbols_WhenNotLoaded_ReturnsErrorJson()
    {
        var plugin = CreatePlugin(); // 无 solution

        var result = plugin.SearchSymbols("anything");

        Assert.True(IsErrorJson(result), $"Expected error JSON, got: {result}");
    }

    [Fact]
    public void GetSymbolDetail_WhenNotLoaded_ReturnsErrorJson()
    {
        var plugin = CreatePlugin();

        var result = plugin.GetSymbolDetail("T:Any.Type");

        Assert.True(IsErrorJson(result));
    }

    [Fact]
    public void GetTypeMembers_WhenNotLoaded_ReturnsErrorJson()
    {
        var plugin = CreatePlugin();

        var result = plugin.GetTypeMembers("T:Any.Type");

        Assert.True(IsErrorJson(result));
    }

    [Fact]
    public async Task GetTypeHierarchyAsync_WhenNotLoaded_ReturnsErrorJson()
    {
        var plugin = CreatePlugin();

        var result = await plugin.GetTypeHierarchyAsync("T:Any.Type");

        Assert.True(IsErrorJson(result));
    }

    [Fact]
    public async Task GetInterfaceImplementationsAsync_WhenNotLoaded_ReturnsErrorJson()
    {
        var plugin = CreatePlugin();

        var result = await plugin.GetInterfaceImplementationsAsync("T:Any.Interface");

        Assert.True(IsErrorJson(result));
    }

    [Fact]
    public async Task GetCallersAsync_WhenRoslynUnavailable_ReturnsErrorJson()
    {
        // solution 已加载但 RoslynSolution 为 null（SetForTesting 行为）
        var plugin = CreateRichPlugin();

        var result = await plugin.GetCallersAsync(SymbolSemanticFixtures.SaveAsyncId);

        Assert.True(IsErrorJson(result), $"Expected error JSON, got: {result}");
    }

    [Fact]
    public async Task GetCalleesAsync_WhenRoslynUnavailable_ReturnsErrorJson()
    {
        var plugin = CreateRichPlugin();

        var result = await plugin.GetCalleesAsync(SymbolSemanticFixtures.SaveAsyncId);

        Assert.True(IsErrorJson(result));
    }

    [Fact]
    public async Task GetUsagesAsync_WhenRoslynUnavailable_ReturnsErrorJson()
    {
        var plugin = CreateRichPlugin();

        var result = await plugin.GetUsagesAsync(SymbolSemanticFixtures.UserServiceId);

        Assert.True(IsErrorJson(result));
    }

    // ══════════════════════════════════════════════════════════════════════
    // SearchSymbols
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SearchSymbols_ValidQuery_ReturnsJsonArray()
    {
        var plugin = CreateRichPlugin();

        var result = plugin.SearchSymbols("UserService");

        Assert.True(IsArrayJson(result), $"Expected JSON array, got: {result}");
    }

    [Fact]
    public void SearchSymbols_ValidQuery_EachItemHasRequiredFields()
    {
        var plugin = CreateRichPlugin();

        var result = plugin.SearchSymbols("UserService", topK: 5);

        var arr = JsonNode.Parse(result)!.AsArray();
        Assert.NotEmpty(arr);

        foreach (var item in arr)
        {
            Assert.NotNull(item!["symbolId"]);
            Assert.NotNull(item["name"]);
            Assert.NotNull(item["kind"]);
            Assert.NotNull(item["signature"]);
            Assert.NotNull(item["score"]);
        }
    }

    [Fact]
    public void SearchSymbols_TopKIsRespected_ResultCountWithinBound()
    {
        var plugin = CreateRichPlugin();

        var result = plugin.SearchSymbols("e", topK: 2); // "e" 命中很多

        var arr = JsonNode.Parse(result)!.AsArray();
        Assert.True(arr.Count <= 2);
    }

    [Fact]
    public void SearchSymbols_NoMatch_ReturnsEmptyArray()
    {
        var plugin = CreateRichPlugin();

        var result = plugin.SearchSymbols("XyzAbsolutelyNothing");

        var arr = JsonNode.Parse(result)!.AsArray();
        Assert.Empty(arr);
    }

    // ══════════════════════════════════════════════════════════════════════
    // GetSymbolDetail
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetSymbolDetail_ExistingType_ReturnsObjectWithTypeDetail()
    {
        var plugin = CreateRichPlugin();

        var result = plugin.GetSymbolDetail(SymbolSemanticFixtures.UserServiceId);

        var node = ParseJson(result);
        Assert.Equal("UserService", node["name"]!.GetValue<string>());
        Assert.NotNull(node["typeDetail"]);
        Assert.Null(node["memberDetail"]);
    }

    [Fact]
    public void GetSymbolDetail_ExistingMethod_ReturnsObjectWithMemberDetail()
    {
        var plugin = CreateRichPlugin();

        var result = plugin.GetSymbolDetail(SymbolSemanticFixtures.SaveAsyncId);

        var node = ParseJson(result);
        Assert.Equal("SaveAsync", node["name"]!.GetValue<string>());
        Assert.NotNull(node["memberDetail"]);
        Assert.Null(node["typeDetail"]);
        Assert.True(node["memberDetail"]!["isAsync"]!.GetValue<bool>());
    }

    [Fact]
    public void GetSymbolDetail_UnknownId_ReturnsErrorJson()
    {
        var plugin = CreateRichPlugin();

        var result = plugin.GetSymbolDetail("T:Totally.Unknown.Symbol");

        Assert.True(IsErrorJson(result));
    }

    [Fact]
    public void GetSymbolDetail_IncludesLocationsArray()
    {
        var plugin = CreateRichPlugin();

        var result = plugin.GetSymbolDetail(SymbolSemanticFixtures.UserServiceId);

        var node = ParseJson(result);
        var locations = node["locations"]?.AsArray();
        Assert.NotNull(locations);
        Assert.NotEmpty(locations!);
        Assert.NotNull(locations![0]!["filePath"]);
    }

    // ══════════════════════════════════════════════════════════════════════
    // GetTypeMembers
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetTypeMembers_NoFilter_ReturnsAllMembers()
    {
        var plugin = CreateRichPlugin();

        var result = plugin.GetTypeMembers(SymbolSemanticFixtures.UserServiceId);

        var node = ParseJson(result);
        Assert.Equal("UserService", node["name"]!.GetValue<string>());
        Assert.Equal(4, node["totalCount"]!.GetValue<int>());

        var members = node["members"]!.AsArray();
        Assert.Equal(4, members.Count);
    }

    [Fact]
    public void GetTypeMembers_MethodFilter_ReturnsOnlyMethods()
    {
        var plugin = CreateRichPlugin();

        var result = plugin.GetTypeMembers(
            SymbolSemanticFixtures.UserServiceId, kindFilter: "Method");

        var node = ParseJson(result);
        var members = node["members"]!.AsArray();
        Assert.All(members,
            m => Assert.Equal("Method", m!["kind"]!.GetValue<string>()));
    }

    [Fact]
    public void GetTypeMembers_MemberHasSymbolId()
    {
        var plugin = CreateRichPlugin();

        var result = plugin.GetTypeMembers(SymbolSemanticFixtures.UserServiceId, kindFilter: "Method");

        var node = ParseJson(result);
        var members = node["members"]!.AsArray();
        
        Assert.All(members, m => Assert.NotNull(m!["symbolId"]));
    }
}
