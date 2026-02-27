namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class GetFileTreeArgs
{
    public required string RootPath { get; set; }
    public int Depth { get; set; } = 4;
    public List<string>? ExcludePatterns { get; set; }
}