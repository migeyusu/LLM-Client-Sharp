using LLMClient.Dialog.Models;
using LLMClient.Endpoints.Messages;
using Microsoft.Extensions.AI;
using ChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;

namespace LLMClient.Abstraction;

public interface IChatUsage
{
    double? Price { get; }

    UsageDetails? Usage { get; }

    UsageDetails? LastSuccessfulUsage { get; }
}

public interface IResponse : IChatUsage, IChatHistoryItem, ITokenizable
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

    IList<ChatAnnotation>? Annotations { get; set; }

    ChatFinishReason? FinishReason { get; set; }
}