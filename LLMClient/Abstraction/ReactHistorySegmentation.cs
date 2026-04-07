using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public sealed class ReactHistorySegmentation
{
    public List<ChatMessage> PreambleMessages { get; } = [];

    public List<ReactHistoryRound> Rounds { get; } = [];
}

