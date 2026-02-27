namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class RecentFileView
{
    public required string FilePath { get; set; }
    public string? RelativePath { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
    public long SizeBytes { get; set; }
    public string Kind { get; set; } = "Other";
}