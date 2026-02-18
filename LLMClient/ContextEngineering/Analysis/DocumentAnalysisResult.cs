using System.Text.Json.Serialization;

namespace LLMClient.ContextEngineering.Analysis;

public class DocumentAnalysisResult
{
    public required string FilePath { get; set; }
    public List<NamespaceInfo> Namespaces { get; set; } = new();
    public int LinesOfCode { get; set; }
    public int TypeCount { get; set; }
    public int MethodCount { get; set; }

    /// <summary>
    /// 原文件修改时间
    /// </summary>
    [JsonIgnore]
    public DateTime SourceEditTime { get; set; }
}