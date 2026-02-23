using System.ComponentModel;
using System.Text.Json;
using LLMClient.ToolCall;
using Microsoft.SemanticKernel;

namespace LLMClient.ContextEngineering.Tools;

/// <summary>
/// Semantic Kernel Plugin：项目感知工具集。
/// 所有路径参数均使用相对于 solution 根目录的相对路径；
/// 内部由 ProjectAwarenessService 负责与绝对路径的互转。
/// 所有方法统一返回 string（JSON 或错误消息），便于 LLM 直接消费。
/// </summary>
public sealed class ProjectAwarenessPlugin : KernelFunctionGroup
{
    private readonly IProjectAwarenessService _service;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public ProjectAwarenessPlugin(IProjectAwarenessService service) : base("ProjectAwareness")
    {
        _service = service;
    }

    // ─────────────────────────────────────────────────────────────────────

    [KernelFunction("get_solution_info")]
    [Description(
        "Returns a high-level overview of the loaded solution: all projects with their names, " +
        "output types, target frameworks, file counts, and workspace-level conventions. " +
        "Always call this first to orient yourself before exploring any specific project or file. " +
        "The returned project 'name' field can be used directly in get_project_metadata.")]
    public string GetSolutionInfo()
        => Try(() => Serialize(_service.GetSolutionInfoView()));

    // ─────────────────────────────────────────────────────────────────────

    [KernelFunction("get_project_metadata")]
    [Description(
        "Returns detailed metadata for a specific project: package references (NuGet), " +
        "project-to-project references, language version, code statistics (types, methods, LOC), " +
        "and project-level conventions such as Nullable and ImplicitUsings settings. " +
        "Call get_solution_info first to discover available project names.")]
    public string GetProjectMetadata(
        [Description(
            "Project name (e.g. 'MyApp.Core') or solution-relative path to the .csproj file " +
            "(e.g. 'src/MyApp.Core/MyApp.Core.csproj'). " +
            "Project names are listed in get_solution_info output.")]
        string projectId)
        => Try(() => Serialize(_service.GetProjectMetadataView(projectId)));

    // ─────────────────────────────────────────────────────────────────────

    [KernelFunction("get_file_tree")]
    [Description(
        "Returns an ASCII directory tree of all C# source files tracked by the project analysis. " +
        "Only Roslyn-indexed files are shown (build artifacts like obj/ and bin/ are always excluded). " +
        "Use this to understand folder layout and locate files before calling read_file or get_file_metadata. " +
        "Directories are listed before files at each level.")]
    public string GetFileTree(
        [Description(
            "Solution-relative path to explore. " +
            "Use '.' or leave empty to get the full solution tree. " +
            "Narrow down with a subfolder to reduce output, e.g. 'MyApp.Core' or 'src/Features/Auth'.")]
        string path = ".",
        [Description(
            "Maximum folder depth to traverse. Default is 4. " +
            "Increase to 6 only when exploring deeply nested structures; " +
            "higher values produce more tokens.")]
        int maxDepth = 4,
        [Description(
            "Comma-separated path fragments to suppress from the output. " +
            "Matched as case-insensitive substrings against each entry's path. " +
            "Default covers standard build and VCS artifacts.")]
        string excludePatterns = "obj,bin,.vs,.git")
        => Try(() => _service.GetFileTree(path, maxDepth, ParsePatterns(excludePatterns)));

    // ─────────────────────────────────────────────────────────────────────

    [KernelFunction("get_file_metadata")]
    [Description(
        "Returns metadata for a single file: absolute path, solution-relative path, " +
        "file kind (Source/Config/Doc/Generated/Resource), size in bytes, line count, " +
        "and last-modified timestamp. " +
        "Call this before reading a large file to decide whether to read it in full or in sections.")]
    public string GetFileMetadata(
        [Description(
            "Solution-relative path to the file, e.g. 'MyApp.Core/Services/UserService.cs'. " +
            "Obtain valid paths from get_file_tree output. " +
            "Absolute paths are also accepted.")]
        string filePath)
        => Try(() => Serialize(_service.GetFileMetadata(filePath)));

    // ─────────────────────────────────────────────────────────────────────

    [KernelFunction("detect_conventions")]
    [Description(
        "Returns detected code conventions for the loaded workspace: " +
        "whether .editorconfig is present, Nullable reference types setting, ImplicitUsings, " +
        "default namespace style, test framework in use (xUnit/NUnit/MSTest), " +
        "and notable documentation files (README, ADR). " +
        "Consult this before generating new code to match existing project style.")]
    public string DetectConventions()
        => Try(() => Serialize(_service.DetectConventions()));

    // ─────────────────────────────────────────────────────────────────────

    [KernelFunction("get_recently_modified_files")]
    [Description(
        "Returns source files sorted by last-modified time, newest first. " +
        "Use this to quickly identify recently active areas of the codebase, " +
        "which are the most likely locations for a task that touches recent work.")]
    public string GetRecentlyModifiedFiles(
        [Description(
            "ISO-8601 UTC lower bound for last-modified time, e.g. '2025-06-01T00:00:00Z'. " +
            "Omit or pass null to return the newest files regardless of age.")]
        DateTime? sinceUtc = null,
        [Description(
            "Maximum number of files to return. Accepted range: 1–200. Default is 30. " +
            "Use a smaller value (e.g. 10) for a quick orientation; " +
            "use a larger value only when building a comprehensive change inventory.")]
        int count = 30)
        => Try(() => Serialize(_service.GetRecentlyModifiedFiles(sinceUtc, count)));

    // ── 内部工具 ─────────────────────────────────────────────────────────

    private static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, JsonOptions);

    private static string Error(string message)
        => JsonSerializer.Serialize(new { error = message }, JsonOptions);

    /// <summary>
    /// 统一异常处理：将异常转为 LLM 可理解的错误消息，避免 tool call 直接失败。
    /// </summary>
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
    }

    private static List<string> ParsePatterns(string raw)
        => raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    public override string? AdditionPrompt { get; }

    public override object Clone()
    {
        throw new NotImplementedException();
    }
}