// ReadFileResult.cs
namespace LLMClient.ContextEngineering.Tools.Models;

public sealed record ReadFileResult
{
    public required string FilePath { get; init; }
    public string? RelativePath { get; init; }
    public int TotalLines { get; init; }
    /// <summary>实际读取的起始行（1-based，包含）</summary>
    public int StartLine { get; init; }
    /// <summary>实际读取的结束行（1-based，包含）</summary>
    public int EndLine { get; init; }
    public required string Content { get; init; }
    /// <summary>因 maxTokens 限制被截断时为 true；LLM 应缩小行范围后重新调用</summary>
    public bool Truncated { get; init; }
    public int TokenEstimate { get; init; }
}