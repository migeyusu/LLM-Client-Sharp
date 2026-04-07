using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public sealed class ChatHistoryCompressionContext
{
    public required List<ChatMessage> ChatHistory { get; init; }

    public required ReactHistoryCompressionOptions Options { get; init; }

    public required int CurrentRound { get; init; }

    public required ILLMChatClient CurrentClient { get; init; }

    public bool CompressionApplied { get; set; }
}

