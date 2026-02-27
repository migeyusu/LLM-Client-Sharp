namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class FileMetadataView
{
    public required string FilePath { get; set; }
    public string? RelativePath { get; set; }
    public string Extension { get; set; } = string.Empty;
    public string Kind { get; set; } = "Other";

    public long SizeBytes { get; set; }
    public int LinesOfCode { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
}