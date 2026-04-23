using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LLMClient.Component.Utility;
using LLMClient.ContextEngineering.Tools.Models;
using LLMClient.Endpoints;
using LLMClient.ToolCall;
using Microsoft.SemanticKernel;

namespace LLMClient.ContextEngineering.Tools;

/// <summary>
/// Semantic Kernel Plugin：代码修改工具集。
/// 这是整个 ContextEngineering 工具集中唯一具有写副作用的插件。
/// 所有 symbolId 参数必须与 SymbolSemanticPlugin / CodeReadingPlugin 返回的 symbolId 一致。
/// 所有 filePath 参数使用 solution-relative 路径，与其他插件一致。
/// </summary>
public sealed class CodeMutationPlugin : KernelFunctionGroup
{
    private readonly CodeMutationService _service;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CodeMutationPlugin(CodeMutationService service) : base("CodeMutation")
    {
        _service = service;
        _service.RequestDiffApprovalCallback = RequestDiffApprovalAsync;
        _service.RequestPermissionCallback = RequestPermissionAsync;
    }

    private async Task<bool> RequestDiffApprovalAsync(List<CodeMutationFilePreview> previews)
    {
        var current = AsyncContextStore<ChatStackContext>.Current;
        if (current?.AutoApproveAllInvocations == true)
            return true;

        var step = current?.CurrentStep;
        if (step == null)
            return true;

        if (previews.Count == 0)
            return true;

        if (previews.Count == 1)
        {
            var p = previews[0];
            var vm = new FileEditRequestViewModel
            {
                Title = $"Confirm mutation – {p.RelativePath}",
                Description = "Review the proposed code changes before applying them.",
                Path = p.RelativePath,
                AbsolutePath = p.AbsolutePath,
                OriginalContent = p.OriginalContent,
                UpdatedContent = p.UpdatedContent,
                IsReadOnly = true
            };
            return await step.RequestPermissionAsync(vm);
        }

        // Multi-file mutation: aggregate into a single FileEditRequestViewModel
        var sbOriginal = new StringBuilder();
        var sbUpdated = new StringBuilder();
        foreach (var p in previews)
        {
            sbOriginal.AppendLine($"// ===== {p.RelativePath} =====");
            sbOriginal.AppendLine(p.OriginalContent);
            sbOriginal.AppendLine();

            sbUpdated.AppendLine($"// ===== {p.RelativePath} =====");
            sbUpdated.AppendLine(p.UpdatedContent);
            sbUpdated.AppendLine();
        }

        var multiVm = new FileEditRequestViewModel
        {
            Title = $"Confirm mutation – {previews.Count} files",
            Description = $"Review proposed changes across {previews.Count} files.",
            Path = string.Join(", ", previews.Select(p => p.RelativePath)),
            AbsolutePath = previews[0].AbsolutePath,
            OriginalContent = sbOriginal.ToString(),
            UpdatedContent = sbUpdated.ToString(),
            IsReadOnly = true
        };
        return await step.RequestPermissionAsync(multiVm);
    }

    private async Task<bool> RequestPermissionAsync(string message)
    {
        var current = AsyncContextStore<ChatStackContext>.Current;
        if (current?.AutoApproveAllInvocations == true)
            return true;

        var step = current?.CurrentStep;
        if (step == null)
            return true;

        var vm = new ToolCallRequestViewModel
        {
            CallerClassName = nameof(CodeMutationPlugin),
            CallerMethodName = "ApplyChanges",
            Message = message
        };
        return await step.RequestPermissionAsync(vm);
    }

    // ── rename_symbol ─────────────────────────────────────────────────────

    [KernelFunction("rename_symbol")]
    [Description(
        "Renames a symbol (type, method, property, field, parameter, etc.) across the entire solution. " +
        "Uses Roslyn Rename API so that all references are updated automatically, including strings if the symbol " +
        "is used in nameof() or similar contexts. " +
        "Always call get_usages first to understand the blast radius before renaming public APIs.")]
    public async Task<string> RenameSymbolAsync(
        [Description(
            "The symbolId returned by search_symbols or get_symbol_detail. " +
            "Typically the Roslyn documentation comment ID, e.g. 'M:MyApp.Core.UserService.GetUserAsync(System.Int32)'.")]
        string symbolId,
        [Description("The new name for the symbol. Must be a valid C# identifier.")]
        string newName,
        CancellationToken cancellationToken = default)
        => Serialize(await _service.RenameSymbolAsync(symbolId, newName, cancellationToken));

    // ── add_using ─────────────────────────────────────────────────────────

    [KernelFunction("add_using")]
    [Description(
        "Adds a using directive to the top of a C# source file. " +
        "If the using already exists, the operation succeeds with a no-op message. " +
        "Use this when inserting code that references types from another namespace.")]
    public async Task<string> AddUsingAsync(
        [Description(
            "Solution-relative path to the file to modify, e.g. 'MyApp.Core/Services/UserService.cs'. " +
            "Obtain valid paths from list_files or get_file_tree.")]
        string filePath,
        [Description("The namespace to import, e.g. 'System.Collections.Generic' or 'MyApp.Core.Models'.")]
        string namespaceName,
        CancellationToken cancellationToken = default)
        => Serialize(await _service.AddUsingAsync(filePath, namespaceName, cancellationToken));

    // ── delete_symbol ─────────────────────────────────────────────────────

    [KernelFunction("delete_symbol")]
    [Description(
        "Deletes a symbol (member or type) from its containing file. " +
        "The entire declaration including XML documentation comments is removed. " +
        "This does NOT remove references in other files; use get_usages first to clean up callers, " +
        "or delete_symbol is typically used after you have already refactored dependents.")]
    public async Task<string> DeleteSymbolAsync(
        [Description(
            "The symbolId of the member or type to delete. " +
            "Obtain from search_symbols or get_file_outline.")]
        string symbolId,
        CancellationToken cancellationToken = default)
        => Serialize(await _service.DeleteSymbolAsync(symbolId, cancellationToken));

    // ── replace_symbol_body ───────────────────────────────────────────────

    [KernelFunction("replace_symbol_body")]
    [Description(
        "Replaces the entire declaration of a symbol (method, property, type, etc.) with new text. " +
        "The newBody must be a complete, valid C# declaration including modifiers and signature " +
        "(e.g. 'public async Task<User> GetUserAsync(int id) { ... }'). " +
        "Use read_symbol_body first to see the current implementation, then provide the full replacement.")]
    public async Task<string> ReplaceSymbolBodyAsync(
        [Description(
            "The symbolId of the target symbol, from search_symbols or get_symbol_detail.")]
        string symbolId,
        [Description(
            "The complete new declaration text. Must be valid C# syntax.")]
        string newBody,
        CancellationToken cancellationToken = default)
        => Serialize(await _service.ReplaceSymbolBodyAsync(symbolId, newBody, cancellationToken));

    // ── apply_semantic_edit ───────────────────────────────────────────────

    [KernelFunction("apply_semantic_edit")]
    [Description(
        "Applies one or more syntax-aware edits to a single file. " +
        "Each edit locates an existing syntax node by exact text match (oldText) and performs the specified operation. " +
        "Edits are applied sequentially; if any edit fails, the entire batch is aborted and no changes are written. " +
        "Kind values: Replace, Delete, InsertBefore, InsertAfter. " +
        "Use this for complex refactorings that span multiple nearby nodes in the same file.")]
    public async Task<string> ApplySemanticEditAsync(
        [Description(
            "Solution-relative path to the file to edit. " +
            "Obtain valid paths from list_files or get_file_tree.")]
        string filePath,
        [Description(
            "Ordered list of edit operations to apply.")]
        List<SemanticEditOperation> edits,
        CancellationToken cancellationToken = default)
        => Serialize(await _service.ApplySemanticEditAsync(filePath, edits, cancellationToken));

    // ── internals ───────────────────────────────────────────────────────────

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOpts);

    public override string? AdditionPrompt { get; } =
        "CodeMutationPlugin is the ONLY write-capable tool set in the project context. " +
        "It performs syntax-aware modifications via Roslyn and automatically refreshes the symbol index after each change. " +
        "Recommended workflow: 1) read_symbol_body to understand current code. " +
        "2) Use replace_symbol_body for single-member overhauls, or apply_semantic_edit for multi-node surgeries. " +
        "3) Use rename_symbol for safe cross-solution renaming. " +
        "4) Use add_using before inserting code that depends on external namespaces. " +
        "5) Always verify with get_symbol_detail or read_symbol_body after mutation to confirm the result.";

    public override object Clone() => new CodeMutationPlugin(_service);
}
