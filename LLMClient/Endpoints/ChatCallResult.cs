using System.Diagnostics.CodeAnalysis;
using LLMClient.Abstraction;
using LLMClient.Endpoints.Messages;
using Microsoft.Extensions.AI;
using ChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace LLMClient.Endpoints;

public class ChatCallResult : IResponse
{
    public static readonly ChatCallResult Empty = new();

    public UsageDetails? Usage { get; set; }

    public long Tokens
    {
        get { return Usage?.OutputTokenCount ?? 0; }
    }

    public int Latency { get; set; }

    public int Duration { get; set; }

    public Exception? Exception
    {
        get;
        set
        {
            field = value;
            if (value != null)
            {
                ErrorMessage = value.HierarchicalMessage();
            }
        }
    }

    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// 每千个token的平均延迟，单位ms
    /// </summary>
    public float? AvgLatencyPerTokens
    {
        get
        {
            long? usageInputTokenCount = 0;
            if (this.Latency != 0 && this.Usage != null && (usageInputTokenCount = this.Usage.InputTokenCount) > 0)
            {
                return this.Latency / (float)usageInputTokenCount;
            }

            return null;
        }
    }

    [MemberNotNullWhen(true, nameof(Exception))]
    public bool IsCanceled => Exception is OperationCanceledException;

    [MemberNotNullWhen(true, nameof(Exception))]
    public bool IsInvalidRequest => Exception is LlmInvalidRequestException;

    public double? Price { get; set; }

    public bool IsInterrupt
    {
        get { return ErrorMessage != null; }
    }

    public bool IsUnhandledError
    {
        get { return Exception is CriticalException; }
    }

    public ChatFinishReason? FinishReason { get; set; }

    /// <summary>
    /// 有效调用次数
    /// </summary>
    public int ValidCallTimes { get; set; } = 0;

    public string? FirstTextResponse
    {
        get { return Messages?.FirstOrDefault()?.Text; }
    }

    public IEnumerable<ChatMessage> Messages { get; set; } = Enumerable.Empty<ChatMessage>();

    public IList<ChatAnnotation>? Annotations { get; set; }

    public AdditionalPropertiesDictionary? AdditionalProperties { get; set; }

    public string? GetContentAsString()
    {
        return Messages?.Aggregate(string.Empty, (current, message) => current + message.Text);
    }
}