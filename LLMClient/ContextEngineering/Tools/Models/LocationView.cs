namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class LocationView
{
    public string FilePath { get; init; } = "";
    public int StartLine { get; init; }
    public int EndLine { get; init; }
}