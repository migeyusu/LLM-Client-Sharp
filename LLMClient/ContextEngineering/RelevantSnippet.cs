namespace LLMClient.ContextEngineering;

/// <summary>
/// 相关代码片段
/// </summary>
public class RelevantSnippet
{
    public required string SourcePath { get; init; }
    public required string Signature { get; init; }
    public string? Summary { get; init; }
    public required string CodeContent { get; init; }
    public required string Query { get; init; }
    public double RelevanceScore { get; init; }
}