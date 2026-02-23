using System.Text.Json.Serialization;

namespace LLMClient.ContextEngineering.Analysis;

public sealed class FileEntryInfo
{
    public required string FilePath { get; set; }         // absolute
    public required string RelativePath { get; set; }     // relative to project root
    public required string ProjectFilePath { get; set; }  // for traceability

    public string Extension { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int LinesOfCode { get; set; }

    public DateTime LastWriteTimeUtc { get; set; }

    // 简单标签：Source/Generated/Config/Resource/Doc/Other
    public string Kind { get; set; } = "Source";

    // 可选：用于缓存判定、对外可不序列化
    [JsonIgnore]
    public string? Hash { get; set; }
}