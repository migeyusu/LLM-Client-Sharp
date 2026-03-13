// FileListResult.cs
namespace LLMClient.ContextEngineering.Tools.Models;

public sealed record FileListResult
{
    public required string RootPath { get; init; }
    /// <summary>符合条件的文件总数（未截断前）</summary>
    public int TotalCount { get; init; }
    public bool Truncated { get; init; }
    /// <summary>复用 FileMetadataView，包含路径、大小、行数、Kind 等</summary>
    public List<FileMetadataView> Files { get; init; } = [];
}