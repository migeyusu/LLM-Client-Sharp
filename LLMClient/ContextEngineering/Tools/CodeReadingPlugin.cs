// CodeReadingPlugin.cs

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using LLMClient.ToolCall;
using Microsoft.SemanticKernel;

namespace LLMClient.ContextEngineering.Tools;

/// <summary>
/// Semantic Kernel Plugin：代码读取工具集。
/// 所有方法均为只读，无任何文件系统写副作用。
/// </summary>
public sealed class CodeReadingPlugin : KernelFunctionGroup
{
    private readonly ICodeReadingService _service;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CodeReadingPlugin(ICodeReadingService service) : base("CodeReading")
    {
        _service = service;
    }

    // ── read_file ─────────────────────────────────────────────────────────

    [KernelFunction("read_file")]
    [Description(
        "Reads the content of a source file, optionally restricted to a line range. " +
        "If the content exceeds maxTokens, it is truncated at a line boundary and 'truncated: true' is set — " +
        "in that case call again with a narrower startLine/endLine range. " +
        "Call get_file_metadata first to check line count before reading large files. " +
        "Use get_file_outline to locate the lines of interest before reading.")]
    public string ReadFile(
        [Description(
            "Solution-relative or absolute path to the file. " +
            "Obtain valid paths from list_files or get_file_tree.")]
        string path,
        [Description(
            "1-based inclusive start line. Omit to read from the beginning of the file.")]
        int? startLine = null,
        [Description(
            "1-based inclusive end line. Omit to read to the end of the file.")]
        int? endLine = null,
        [Description(
            "Maximum token budget for the returned content. Default is 8000. " +
            "Reduce this when you only need a small section; increase carefully as it affects context size.")]
        int? maxTokens = null)
        => Serialize(_service.ReadFile(path, startLine, endLine, maxTokens));

    // ── read_symbol_body ──────────────────────────────────────────────────

    [KernelFunction("read_symbol_body")]
    [Description(
        "Returns the full implementation body of a named symbol (method, property, type, etc.) " +
        "identified by its symbolId, plus optional surrounding context lines. " +
        "Prefer this over read_file when you know exactly which symbol you need — " +
        "it automatically resolves the correct line range via Roslyn. " +
        "Use search_symbols or get_file_outline to discover valid symbolIds.")]
    public async Task<string> ReadSymbolBodyAsync(
        [Description(
            "The symbolId returned by search_symbols or get_file_outline. " +
            "Typically the Roslyn documentation comment ID, e.g. 'M:MyApp.Core.UserService.GetUserAsync(System.Int32)'.")]
        string symbolId,
        [Description(
            "Number of extra lines to include before and after the symbol body for context. " +
            "Default 0 returns only the declaration. Use 3–5 to see surrounding code.")]
        int contextLines = 0,
        CancellationToken cancellationToken = default)
        => Serialize(await _service.ReadSymbolBodyAsync(symbolId, contextLines, cancellationToken));

    // ── get_file_outline ──────────────────────────────────────────────────

    [KernelFunction("get_file_outline")]
    [Description(
        "Returns the structural outline of a source file: all namespaces, types, and their members " +
        "with signatures, accessibility, XML summary, and start line — but without implementation bodies. " +
        "Use this as the first step when exploring an unfamiliar file, especially large ones. " +
        "After identifying the symbol of interest, call read_symbol_body to fetch the actual implementation.")]
    public string GetFileOutline(
        [Description(
            "Solution-relative or absolute path to the file. " +
            "Obtain valid paths from list_files or get_file_tree.")]
        string path)
        => Serialize(_service.GetFileOutline(path));

    // ── list_files ────────────────────────────────────────────────────────

    [KernelFunction("list_files")]
    [Description(
        "Returns a structured list of source files under a given path, including metadata for each file " +
        "(relative path, kind, size, line count, last modified). " +
        "Use filter to narrow by extension (e.g. '.cs') or filename substring (e.g. 'Service'). " +
        "Prefer get_file_tree for visual navigation; use list_files when you need machine-readable metadata " +
        "to decide which files to read next.")]
    public string ListFiles(
        [Description(
            "Solution-relative path to search under. Use '.' or omit for the entire solution.")]
        string path = ".",
        [Description(
            "Comma-separated filter patterns matched against file name or extension. " +
            "Examples: '.cs' — C# files only; 'Service,.cs' — files named 'Service' OR with .cs extension. " +
            "Omit to return all indexed files under the path.")]
        string? filter = null,
        [Description(
            "True (default) to include files in subdirectories; false to return only direct children.")]
        bool recursive = true,
        [Description(
            "Maximum number of files to return. Accepted range: 1–500. Default is 300. " +
            "When 'truncated: true', narrow the path or add a filter.")]
        int maxCount = 300)
        => Serialize(_service.ListFiles(path, filter, recursive, maxCount));

    // ── 内部工具（与 ProjectAwarenessPlugin 保持一致的模式）──────────────

    private static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, JsonOptions);


    public override string? AdditionPrompt { get; } =
        "CodeReadingPlugin provides read-only access to source file contents and structural outlines. " +
        "Recommended workflow: list_files → get_file_outline → read_symbol_body. " +
        "Fall back to read_file only when you need content not captured by the symbol index " +
        "(e.g. top-level statements, config files, or auto-generated code).";

    public override object Clone() => new CodeReadingPlugin(_service);
}