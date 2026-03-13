// SymbolBodyView.cs
namespace LLMClient.ContextEngineering.Tools.Models;

public sealed record SymbolBodyView : SourcedSymbolViewBase
{
    public required string FilePath { get; init; }
    public string? RelativePath { get; init; }
    /// <summary>符号声明本体的起止行（不含 contextLines 扩展）</summary>
    public int BodyStartLine { get; init; }
    public int BodyEndLine { get; init; }
    /// <summary>实际返回内容的起止行（含 contextLines 扩展）</summary>
    public int ContentStartLine { get; init; }
    public int ContentEndLine { get; init; }
    public required string Content { get; init; }
    public int TokenEstimate { get; init; }
}