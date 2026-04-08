using System.Text;

namespace LLMClient.Abstraction;

/// <summary>
/// 单轮 ReAct 循环的结果（在 ReactStep 的 Channel 完成后可用）
/// </summary>
public sealed class StepResult : CallResult
{
    public bool IsCompleted { get; set; } = true;

    public int MaxContextTokens { get; init; }

    public StringBuilder HistoryBuilder { get; } = new();
}