namespace LLMClient.ContextEngineering.Tools.Models;

public sealed record GetRecentlyModifiedFilesArgs
{
    public DateTime? SinceUtc { get; set; }
    public int Count { get; set; } = 30;
}