using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public sealed class ReactHistoryRound
{
    public required int RoundNumber { get; init; }

    public List<ChatMessage> AssistantMessages { get; } = [];

    public List<ChatMessage> ObservationMessages { get; } = [];

    public IEnumerable<ChatMessage> Messages => AssistantMessages.Concat(ObservationMessages);

    public bool HasError => ObservationMessages.Any(m => m.Contents.OfType<FunctionResultContent>().Any(r => r.Exception != null));
}
