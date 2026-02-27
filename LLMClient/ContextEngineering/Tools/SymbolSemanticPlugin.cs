using System.ComponentModel;
using System.Text.Json;
using LLMClient.ToolCall;
using Microsoft.SemanticKernel;

namespace LLMClient.ContextEngineering.Tools;

/// <summary>
/// Semantic Kernel Plugin：符号与语义分析工具集。
/// 所有 symbolId 均来自本 Plugin 或 ProjectAwarenessPlugin 的返回结果。
/// 异步工具通过 async KernelFunction 暴露，SK 框架自动处理 Task。
/// </summary>
public sealed class SymbolSemanticPlugin : KernelFunctionGroup
{
    private readonly SymbolSemanticService _service;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public SymbolSemanticPlugin(SymbolSemanticService service) : base("SymbolSemantic")
    {
        _service = service;
    }

    // ── search_symbols ────────────────────────────────────────────────────

    [KernelFunction("search_symbols")]
    [Description(
        "Search for symbols (types, methods, properties, fields, events) across the entire solution. " +
        "Returns a ranked list of matches with symbolId, signature, location, and XML summary. " +
        "Use the returned 'symbolId' in all other SymbolSemantic tools. " +
        "Prefer exact type or method names; use scope to narrow results to a project or subfolder.")]
    public string SearchSymbols(
        [Description(
            "Name fragment or keyword to search for. Case-insensitive. " +
            "Examples: 'UserService', 'SaveAsync', 'IRepository'.")]
        string query,
        [Description(
            "Optional symbol kind filter. Accepted values: Class, Interface, Struct, Record, " +
            "Method, Property, Field, Constructor, Event. Omit to search all kinds.")]
        string? kind = null,
        [Description(
            "Optional scope filter: a file path fragment (e.g. 'Services') or namespace prefix " +
            "(e.g. 'MyApp.Core'). Omit to search the full solution.")]
        string? scope = null,
        [Description("Maximum results to return. Range: 1–100. Default: 20.")]
        int topK = 20)
        => Try(() => Serialize(_service.SearchSymbols(query, kind, scope, topK)));

    // ── get_symbol_detail ─────────────────────────────────────────────────

    [KernelFunction("get_symbol_detail")]
    [Description(
        "Returns full detail for a single symbol: signature, XML documentation, accessibility, " +
        "attributes, all source locations, and type-specific extras " +
        "(base types / interfaces / member count for types; parameters / return type for methods). " +
        "Use this after search_symbols to inspect a specific symbol before reading its implementation.")]
    public string GetSymbolDetail(
        [Description(
            "The 'symbolId' value from a previous search_symbols or get_type_members response. " +
            "Typically the documentation comment ID, e.g. 'M:MyApp.Core.UserService.SaveAsync(MyApp.Core.User)'.")]
        string symbolId)
        => Try(() => Serialize(_service.GetSymbolDetail(symbolId)));

    // ── get_type_members ──────────────────────────────────────────────────

    [KernelFunction("get_type_members")]
    [Description(
        "Lists all members of a type (methods, properties, constructors, fields, events) " +
        "with their signatures, return types, accessibility, and source locations. " +
        "Supports filtering by member kind and accessibility. " +
        "Call this to understand a type's API surface before writing code that uses or extends it.")]
    public string GetTypeMembers(
        [Description(
            "The symbolId of the target type, from search_symbols or get_symbol_detail. " +
            "A plain type name such as 'UserService' is also accepted when unambiguous.")]
        string typeId,
        [Description(
            "Optional member kind filter: Method, Property, Field, Constructor, Event. Omit for all.")]
        string? kindFilter = null,
        [Description(
            "Optional accessibility filter: Public, Protected, Internal, Private. " +
            "Partial matches are accepted, e.g. 'Public' matches 'Public'.")]
        string? accessibilityFilter = null)
        => Try(() => Serialize(_service.GetTypeMembers(typeId, kindFilter, accessibilityFilter)));

    // ── get_type_hierarchy ────────────────────────────────────────────────

    [KernelFunction("get_type_hierarchy")]
    [Description(
        "Returns the full inheritance chain for a type: ordered base class chain up to object, " +
        "implemented interfaces, and all known derived types within the solution. " +
        "For interfaces, derived types are all implementing classes. " +
        "Prefer this over manually scanning files to understand polymorphism or plan overrides.")]
    public async Task<string> GetTypeHierarchyAsync(
        [Description("symbolId or plain name of the class or interface to inspect.")]
        string typeId,
        CancellationToken cancellationToken = default)
        => await TryAsync(() => _service.GetTypeHierarchyAsync(typeId, cancellationToken)
            .ContinueWith(t => Serialize(t.Result), cancellationToken));

    // ── get_interface_implementations ─────────────────────────────────────

    [KernelFunction("get_interface_implementations")]
    [Description(
        "Finds all types within the solution that implement a given interface. " +
        "More precise than text search: uses Roslyn SymbolFinder when available, " +
        "falls back to static type index. " +
        "Use this to discover injection candidates or to assess the blast radius of an interface change.")]
    public async Task<string> GetInterfaceImplementationsAsync(
        [Description("symbolId or plain name of the interface, e.g. 'IUserRepository'.")]
        string interfaceId,
        CancellationToken cancellationToken = default)
        => await TryAsync(() => _service.GetInterfaceImplementationsAsync(interfaceId, cancellationToken)
            .ContinueWith(t => Serialize(t.Result), cancellationToken));

    // ── get_callers ────────────────────────────────────────────────────────

    [KernelFunction("get_callers")]
    [Description(
        "Finds all methods in the solution that directly call the specified symbol. " +
        "Returns caller signatures and exact call-site locations. " +
        "Use this before renaming, refactoring, or changing a method signature " +
        "to understand downstream impact. Scope can restrict results to one project or folder.")]
    public async Task<string> GetCallersAsync(
        [Description("symbolId of the method or constructor to find callers for.")]
        string symbolId,
        [Description(
            "Optional file-path fragment to restrict results, e.g. 'Controllers' or 'MyApp.Api'. " +
            "Omit to search the full solution.")]
        string? scope = null,
        CancellationToken cancellationToken = default)
        => await TryAsync(() => _service.GetCallersAsync(symbolId, scope, cancellationToken)
            .ContinueWith(t => Serialize(t.Result), cancellationToken));

    // ── get_callees ────────────────────────────────────────────────────────

    [KernelFunction("get_callees")]
    [Description(
        "Lists all methods and constructors invoked inside the body of the specified symbol. " +
        "Useful for understanding a method's dependencies before refactoring, " +
        "or to trace an execution path without reading the full implementation. " +
        "Object creations (new T()) are also included.")]
    public async Task<string> GetCalleesAsync(
        [Description("symbolId of the method or constructor whose body to analyse.")]
        string symbolId,
        CancellationToken cancellationToken = default)
        => await TryAsync(() => _service.GetCalleesAsync(symbolId, cancellationToken)
            .ContinueWith(t => Serialize(t.Result), cancellationToken));

    // ── get_usages ─────────────────────────────────────────────────────────

    [KernelFunction("get_usages")]
    [Description(
        "Finds every reference to a symbol across the solution, including reads, writes, " +
        "and implicit usages. Returns file path, line number, column, " +
        "a trimmed code snippet, and usage kind (Read/Write/Implicit). " +
        "Results are capped at 200; 'truncated: true' signals there are more. " +
        "Use this to audit all usage sites before modifying a shared symbol.")]
    public async Task<string> GetUsagesAsync(
        [Description("symbolId of the type, method, property, or field to find usages for.")]
        string symbolId,
        CancellationToken cancellationToken = default)
        => await TryAsync(() => _service.GetUsagesAsync(symbolId, cancellationToken)
            .ContinueWith(t => Serialize(t.Result), cancellationToken));

    // ── get_dependency_graph ───────────────────────────────────────────────

    [KernelFunction("get_dependency_graph")]
    [Description(
        "Returns a dependency graph of projects and NuGet packages. " +
        "Nodes represent projects (Kind=Project) or packages (Kind=Package). " +
        "Edges represent ProjectReference or PackageReference relationships. " +
        "Use this to understand module coupling before adding a cross-project dependency, " +
        "or to render an architecture diagram.")]
    public string GetDependencyGraph(
        [Description(
            "Project name to use as the root. Omit to include all projects in the solution.")]
        string? projectName = null,
        [Description(
            "How many hops of project references to follow. Range: 1–5. Default: 2. " +
            "Packages are only shown for the root project regardless of depth.")]
        int depth = 2)
        => Try(() => Serialize(_service.GetDependencyGraph(projectName, depth)));

    // ── get_namespace_types ────────────────────────────────────────────────

    [KernelFunction("get_namespace_types")]
    [Description(
        "Lists all types declared within a namespace (and optionally its sub-namespaces). " +
        "Returns type name, kind, signature, accessibility, XML summary, member count, and location. " +
        "Use this to get an overview of a logical module before diving into individual types.")]
    public string GetNamespaceTypes(
        [Description(
            "Exact namespace name, e.g. 'MyApp.Core.Services'. " +
            "Obtain namespaces from get_solution_info or search_symbols results.")]
        string namespaceName,
        [Description(
            "When true (default), also includes types in child namespaces such as " +
            "'MyApp.Core.Services.Auth'. Set to false for strict namespace-only results.")]
        bool includeSubNamespaces = true)
        => Try(() => Serialize(_service.GetNamespaceTypes(namespaceName, includeSubNamespaces)));

    // ── 内部工具 ─────────────────────────────────────────────────────────

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOpts);

    private static string Try(Func<string> action)
    {
        try { return action(); }
        catch (ArgumentException ex) { return Error(ex.Message); }
        catch (InvalidOperationException ex) { return Error(ex.Message); }
        catch (Exception ex) { return Error($"Unexpected error: {ex.Message}"); }
    }

    private static async Task<string> TryAsync(Func<Task<string>> action)
    {
        try { return await action(); }
        catch (ArgumentException ex) { return Error(ex.Message); }
        catch (InvalidOperationException ex) { return Error(ex.Message); }
        catch (OperationCanceledException) { return Error("Operation was cancelled."); }
        catch (Exception ex) { return Error($"Unexpected error: {ex.Message}"); }
    }

    private static string Error(string msg)
        => JsonSerializer.Serialize(new { error = msg }, JsonOpts);

    public override string? AdditionPrompt { get; } =
        "SymbolSemanticPlugin provides deep code intelligence for the loaded solution. " +
        "Workflow: 1) Use search_symbols to get a symbolId. " +
        "2) Use get_symbol_detail / get_type_members for structure inspection. " +
        "3) Use get_callers / get_callees / get_usages for impact analysis. " +
        "4) Use get_type_hierarchy / get_interface_implementations for polymorphism analysis. " +
        "All symbolId values are opaque strings — always source them from tool responses, never construct them manually.";

    public override object Clone() => new SymbolSemanticPlugin(_service);
}