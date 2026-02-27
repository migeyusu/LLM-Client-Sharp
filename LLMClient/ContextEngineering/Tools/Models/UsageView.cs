namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class UsageView
{
    public string FilePath { get; init; } = "";
    public int Line { get; init; }
    public int Column { get; init; }
    /// <summary>该行源码（去除前导空白）</summary>
    public string Snippet { get; init; } = "";
    /// <summary>Read | Write | Implicit</summary>
    public string UsageKind { get; init; } = "";
}