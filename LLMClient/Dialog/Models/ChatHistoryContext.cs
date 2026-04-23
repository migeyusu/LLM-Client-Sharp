using LLMClient.Abstraction;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;

public sealed class ChatHistoryContext
{
    public required SegmentedHistory History { get; init; }

    public IEnumerable<ChatMessage> RequestMessages => History.AllMessages;

    public required ReactHistoryCompressionOptions Options { get; init; }

    public int CurrentRoundNumber { get; set; }

    public long? CurrentTokens { get; set; }

    public required ILLMChatClient CurrentClient { get; init; }

    /// <summary>
    /// 可选：当前 ReAct 轮次的 ReactStep，供压缩策略发射中间进度事件。
    /// </summary>
    public ReactStep? Step { get; set; }

    public bool CompressionApplied { get; set; }
}