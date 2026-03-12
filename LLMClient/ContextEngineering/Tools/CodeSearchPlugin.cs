// File: LLMClient/ContextEngineering/Tools/CodeSearchPlugin.cs

using System.ComponentModel;
using System.Text.Json;
using LLMClient.ToolCall;
using Microsoft.SemanticKernel;

namespace LLMClient.ContextEngineering.Tools;

/// <summary>
/// Semantic Kernel Plugin：代码搜索工具集。
/// 提供文本搜索、语义搜索、相似代码查找、特性标注查找等功能。
/// </summary>
internal sealed class CodeSearchPlugin : KernelFunctionGroup
{
    private readonly CodeSearchService _service;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public CodeSearchPlugin(CodeSearchService service) : base("CodeSearch")
    {
        _service = service;
    }

    // ─────────────────────────────────────────────────────────────────────

    [KernelFunction("search_text")]
    [Description(
        "Performs full-text or regex search across all indexed source files in the solution. " +
        "Returns matching lines with file path, line number, column, and optional surrounding context. " +
        "Empty or whitespace-only patterns return zero results. " + 
        "Use this to locate specific code patterns, variable names, method calls, or error messages. " +
        "For semantic intent-based search, use search_semantic instead.")]
    public string SearchText(
        [Description(
            "Search pattern: plain text or regex pattern (if useRegex=true). " +
            "Must be non-empty. " + 
            "Examples: 'UserService', 'async Task.*HttpClient', 'TODO:'. " +
            "Case-insensitive by default.")]
        string pattern,
        [Description(
            "Optional solution-relative path to limit search scope, e.g. 'MyApp.Core' or 'src/Features'. " +
            "Leave empty to search entire solution.")]
        string? scope = null,
        [Description(
            "Comma-separated file extensions to filter, e.g. '.cs,.xaml'. " +
            "Omit or leave empty to search all file types.")]
        string? fileFilter = null,
        [Description(
            "If true, treat 'pattern' as a regex pattern. Default is false (plain text search).")]
        bool useRegex = false,
        [Description(
            "Number of context lines to include before/after each match (0-3). Default is 1. " +
            "Higher values provide more context but increase token usage.")]
        int contextLines = 1)
        => Try(() =>
            Serialize(_service.SearchText(pattern, scope, fileFilter, useRegex, Math.Clamp(contextLines, 0, 3))));

    // ─────────────────────────────────────────────────────────────────────

    [KernelFunction("search_semantic")]
    [Description(
        "Performs semantic (embedding-based) search to find code snippets matching the conceptual intent of the query. " +
        "This is more powerful than text search for discovering implementations by behavior description. " +
        "Example queries: 'user authentication logic', 'database connection handling', 'HTTP retry mechanism'. " +
        "Falls back to keyword search if RAG/embedding service is unavailable.")]
    public async Task<string> SearchSemanticAsync(
        [Description(
            "Natural language query describing the desired functionality or concept. " +
            "Be specific: 'methods that validate email addresses' is better than 'validation'.")]
        string query,
        [Description(
            "Maximum number of results to return (1-50). Default is 20. " +
            "Use lower values (e.g. 5-10) for focused exploration.")]
        int topK = 20)
        => await TryAsync(async () => Serialize(await _service.SearchSemanticAsync(query, topK)));

    // ─────────────────────────────────────────────────────────────────────

    [KernelFunction("find_similar_code")]
    [Description(
        "Finds code snippets semantically similar to a provided code sample. " +
        "Useful for discovering duplicated logic, existing implementations of similar patterns, " +
        "or alternative approaches to the same problem. " +
        "Pass a short code snippet (5-30 lines) as the query.")]
    public async Task<string> FindSimilarCodeAsync(
        [Description(
            "Code snippet to match against. Include key logic but omit boilerplate. " +
            "Example: 'var result = await httpClient.GetAsync(url); result.EnsureSuccessStatusCode();'")]
        string codeSnippet,
        [Description(
            "Maximum number of similar snippets to return (1-50). Default is 10.")]
        int topK = 10)
        => await TryAsync(async () => Serialize(await _service.FindSimilarCodeAsync(codeSnippet, topK)));

    // ─────────────────────────────────────────────────────────────────────

    [KernelFunction("find_by_attribute")]
    [Description(
        "Finds all types and members annotated with a specific attribute. " +
        "Returns symbol details including location, signature, and all applied attributes. " +
        "Useful for discovering extension points, test methods, serialization markers, etc. " +
        "Works with or without the 'Attribute' suffix: both 'Obsolete' and 'ObsoleteAttribute' are valid.")]
    public string FindByAttribute(
        [Description(
            "Attribute name to search for, e.g. 'Obsolete', 'DataMember', 'TestMethod', 'Route'. " +
            "Case-insensitive. The 'Attribute' suffix is optional.")]
        string attributeName,
        [Description(
            "Optional project name to limit search scope, e.g. 'MyApp.Core'. " +
            "Leave empty to search entire solution.")]
        string? scope = null)
        => Try(() => Serialize(_service.FindByAttribute(attributeName, scope)));

    // ─────────────────────────────────────────────────────────────────────

    [KernelFunction("search_in_file")]
    [Description(
        "Performs text or regex search within a single file. " +
        "Returns all matches with line numbers, column positions, and optional context. " +
        "Use this for focused inspection after narrowing down with search_text or get_file_tree. " +
        "More efficient than search_text when you already know the target file.")]
    public string SearchInFile(
        [Description(
            "Solution-relative path to the file, e.g. 'MyApp.Core/Services/UserService.cs'. " +
            "Obtain valid paths from get_file_tree or search_text results.")]
        string filePath,
        [Description(
            "Search pattern: plain text or regex pattern (if useRegex=true). " +
            "Same semantics as search_text.")]
        string pattern,
        [Description(
            "If true, treat 'pattern' as a regex. Default is false.")]
        bool useRegex = false,
        [Description(
            "Number of context lines before/after each match (0-3). Default is 1.")]
        int contextLines = 1)
        => Try(() => Serialize(_service.SearchInFile(filePath, pattern, useRegex, Math.Clamp(contextLines, 0, 3))));

    // ── 内部工具 ─────────────────────────────────────────────────────────

    private static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, JsonOptions);

    private static string Error(string message)
        => JsonSerializer.Serialize(new { error = message }, JsonOptions);

    private static string Try(Func<string> action)
    {
        try
        {
            return action();
        }
        catch (FileNotFoundException ex)
        {
            return Error($"File not found: {ex.Message}");
        }
        catch (DirectoryNotFoundException ex)
        {
            return Error($"Directory not found: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            return Error(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Error(ex.Message);
        }
        catch (Exception ex)
        {
            return Error($"Search failed: {ex.Message}");
        }
    }

    private static async Task<string> TryAsync(Func<Task<string>> action)
    {
        try
        {
            return await action();
        }
        catch (FileNotFoundException ex)
        {
            return Error($"File not found: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            return Error(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Error(ex.Message);
        }
        catch (Exception ex)
        {
            return Error($"Semantic search failed: {ex.Message}");
        }
    }

    public override string? AdditionPrompt { get; } =
        "CodeSearchPlugin provides powerful search capabilities across the codebase: " +
        "text/regex search, semantic search by intent, similarity detection, and attribute-based filtering. " +
        "Use search_text for exact matches, search_semantic for conceptual queries, " +
        "find_similar_code to detect duplication, and find_by_attribute to discover extension points. " +
        "All methods return JSON with file paths, line numbers, and code snippets.";

    public override object Clone()
    {
        return new CodeSearchPlugin(_service);
    }
}