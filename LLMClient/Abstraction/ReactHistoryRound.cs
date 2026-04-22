using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public sealed class ReactHistoryRound
{
    public required int RoundNumber { get; init; }
    
    public ChatMessage? AssistantMessage { get; set; }

    public ChatMessage? ObservationMessage { get; set; }

    public bool IsValid
    {
        get { return AssistantMessage != null && ObservationMessage != null; }
    }

    public IEnumerable<ChatMessage> Messages
    {
        get
        {
            if (AssistantMessage != null)
            {
                yield return AssistantMessage;
            }

            if (ObservationMessage != null)
            {
                yield return ObservationMessage;
            }
        }
    }

    public bool HasError =>
        ObservationMessage?.Contents.OfType<FunctionResultContent>().Any(r => r.Exception != null) == true;
}