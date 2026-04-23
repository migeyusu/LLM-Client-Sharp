using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public sealed class ReactRound
{
    public required int RoundNumber { get; init; }

    public bool IsCompressApplied { get; set; }

    public ChatMessage? AssistantMessage { get; set; }

    public ChatMessage? ObservationMessage { get; set; }

    [MemberNotNullWhen(true, nameof(AssistantMessage), nameof(ObservationMessage))]
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

    public bool IsErrorRound =>
        ObservationMessage?.Contents.OfType<FunctionResultContent>().All(r => r.Exception != null) == true;
}