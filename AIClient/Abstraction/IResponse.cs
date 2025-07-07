using Microsoft.Extensions.AI;

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

    ChatFinishReason? FinishReason { get; set; }
}