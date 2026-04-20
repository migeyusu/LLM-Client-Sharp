using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public sealed class ChatHistoryCompressionContext
{
    public required List<ChatMessage> ChatHistory { get; init; }

    public required ReactHistoryCompressionOptions Options { get; init; }

    public required int CurrentRound { get; init; }

    public required ILLMChatClient CurrentClient { get; init; }

    /// <summary>
    /// 可选：当前 ReAct 轮次的 ReactStep，供压缩策略发射中间进度事件。
    /// </summary>
    public ReactStep? Step { get; init; }

    public bool CompressionApplied { get; set; }

    /// <summary>
    /// 可选的 Agent 标识。在多 Agent 场景下，压缩策略会基于该 ID 过滤属于其他 Agent 的 ReAct 轮次。
    /// </summary>
    public string? AgentId { get; init; }
}

