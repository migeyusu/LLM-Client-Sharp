using Microsoft.Extensions.AI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace LLMClient.Abstraction;

/// <summary>
/// 单轮 ReAct 循环的结果（在 ReactStep 的 Channel 完成后可用）
/// </summary>
public sealed class StepResult
{
    public UsageDetails? Usage { get; init; }

    public ChatFinishReason? FinishReason { get; init; }

    public int LatencyMs { get; init; }

    public bool IsCompleted { get; init; }

    public int MaxContextTokens { get; init; }

    public List<ChatMessage> Messages { get; init; } = [];

    public Exception? Exception { get; init; }
}

