using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public sealed class ReactHistoryRound
{
    public required int RoundNumber { get; init; }

    public List<ChatMessage> AssistantMessages { get; } = [];

    public List<ChatMessage> ObservationMessages { get; } = [];

    public IEnumerable<ChatMessage> Messages => AssistantMessages.Concat(ObservationMessages);
}

