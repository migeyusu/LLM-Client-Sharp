namespace LLMClient.ContextEngineering.Tools.Models;

/// <summary>
/// Result of a code mutation operation.
/// All file paths are returned as solution-relative strings.
/// </summary>
public record MutationResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public List<string> AffectedFiles { get; init; } = [];
    public string? Message { get; init; }

    public static MutationResult Ok(List<string> affectedFiles, string? message = null)
        => new() { Success = true, AffectedFiles = affectedFiles, Message = message };

    public static MutationResult Fail(string error)
        => new() { Success = false, Error = error };
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
