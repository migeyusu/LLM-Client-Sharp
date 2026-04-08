using LLMClient.Dialog.Models;
using LLMClient.Endpoints.Messages;
using ChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;

namespace LLMClient.Abstraction;

public interface IResponse : IChatUsage, IChatHistoryItem, ITokenizable
{
    /// <summary>
    /// The latency of the response in ms
    /// </summary>
    int Latency { get; }

    /// <summary>
    /// The duration of the response in s, contains latency
    /// </summary>
    int Duration { get; }

    bool IsInterrupt { get; }

    string? ErrorMessage { get; }

    IList<ChatAnnotation>? Annotations { get; set; }

    ChatFinishReason? FinishReason { get; set; }
}