namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class GetRecentlyModifiedFilesArgs
{
    public DateTime? SinceUtc { get; set; }
    public int Count { get; set; } = 30;
}