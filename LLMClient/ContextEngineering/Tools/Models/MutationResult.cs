namespace LLMClient.ContextEngineering.Tools.Models;

/// <summary>
/// Result of a successful code mutation operation.
/// All file paths are returned as solution-relative strings.
/// </summary>
public record MutationResult
{
    public bool Success { get; init; } = true;
    public List<string> AffectedFiles { get; init; } = [];
    public string? Message { get; init; }

    public static MutationResult Ok(List<string> affectedFiles, string? message = null)
        => new() { AffectedFiles = affectedFiles, Message = message };
}

/// <summary>
/// Preview of a single file change before it is written to disk.
/// Passed to the diff approval callback in <see cref="CodeMutationService"/>.
/// </summary>
public record CodeMutationFilePreview
{
    public required string RelativePath { get; init; }
    public required string AbsolutePath { get; init; }
    public required string OriginalContent { get; init; }
    public required string UpdatedContent { get; init; }
}

/// <summary>
/// A single semantic edit operation for <see cref="CodeMutationService.ApplySemanticEditAsync"/>.
/// </summary>
public record SemanticEditOperation
{
    /// <summary>
    /// Edit kind. Accepted values: Replace, Delete, InsertBefore, InsertAfter.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// For Replace/Delete: the exact text of the existing syntax node to locate.
    /// For InsertBefore/InsertAfter: the anchor node text.
    /// </summary>
    public string? OldText { get; init; }

    /// <summary>
    /// For Replace/InsertBefore/InsertAfter: the new syntax text.
    /// </summary>
    public string? NewText { get; init; }
}
