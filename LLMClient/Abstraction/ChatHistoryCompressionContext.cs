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
}

