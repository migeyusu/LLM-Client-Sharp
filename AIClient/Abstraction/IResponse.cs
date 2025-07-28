using LLMClient.Endpoints.Messages;
using OpenAI.Chat;
using ChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace LLMClient.Abstraction;

public interface IResponse : ITokenizable
{
    /// <summary>
    /// The latency of the response in ms
    /// </summary>
    int Latency { get; }

    /// <summary>
    /// The duration of the response in s
    /// </summary>
    int Duration { get; }

    bool IsInterrupt { get; }

    string? ErrorMessage { get; }

    double? Price { get; }

    IList<ChatMessage>? ResponseMessages { get; }

    IList<ChatAnnotation>? Annotations { get; set; }

    ChatFinishReason? FinishReason { get; set; }
}