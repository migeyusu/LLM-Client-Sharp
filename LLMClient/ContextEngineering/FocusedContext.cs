namespace LLMClient.ContextEngineering;

/// <summary>
/// 当前聚焦的上下文（用户正在编辑的位置）
/// </summary>
public class FocusedContext
{
    public required string FilePath { get; init; }

    public required DocumentAnalysisResult DocumentAnalysis { get; set; }
}