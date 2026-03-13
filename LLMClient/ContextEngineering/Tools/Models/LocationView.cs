namespace LLMClient.ContextEngineering.Tools.Models;

public sealed record LocationView
{
    public string FilePath { get; init; } = "";
    public int StartLine { get; init; }
    public int EndLine { get; init; }
}