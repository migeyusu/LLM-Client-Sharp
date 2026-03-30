using System.Diagnostics.CodeAnalysis;
using System.Text;
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

    /// <summary>
    /// 最后一次成功调用返回的 usage，不参与多轮累计。
    /// </summary>
    public UsageDetails? LastSuccessfulUsage { get; set; }

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

    public StringBuilder? History { get; set; }

    public string? GetContentAsString()
    {
        return Messages?.Aggregate(string.Empty, (current, message) => current + message.Text);
    }

    public static ChatCallResult operator +(ChatCallResult? left, ChatCallResult? right)
    {
        if (left is null || left == Empty) return right ?? Empty;
        if (right is null || right == Empty) return left;

        var usage = left.Usage != null || right.Usage != null
            ? new UsageDetails
            {
                InputTokenCount = (left.Usage?.InputTokenCount ?? 0) + (right.Usage?.InputTokenCount ?? 0),
                OutputTokenCount = (left.Usage?.OutputTokenCount ?? 0) + (right.Usage?.OutputTokenCount ?? 0),
                TotalTokenCount = (left.Usage?.TotalTokenCount ?? 0) + (right.Usage?.TotalTokenCount ?? 0),
            }
            : null;

        var result = new ChatCallResult
        {
            Usage = usage,
            LastSuccessfulUsage = right.ValidCallTimes > 0
                ? CloneUsageDetails(right.LastSuccessfulUsage)
                : CloneUsageDetails(left.LastSuccessfulUsage),
            Latency = left.Latency, // Latency取第一个
            Duration = left.Duration + right.Duration,
            Exception = left.Exception ?? right.Exception, // Exception取存在的第一个
            FinishReason = right.FinishReason ?? left.FinishReason, // FinishReason选最后一个
            ValidCallTimes = left.ValidCallTimes + right.ValidCallTimes,
            Price = (left.Price ?? 0) + (right.Price ?? 0),
            Messages = left.Messages.Concat(right.Messages)
                .ToList()
        };

        if (left.Annotations != null || right.Annotations != null)
        {
            var annotations = new List<ChatAnnotation>();
            if (left.Annotations != null) annotations.AddRange(left.Annotations);
            if (right.Annotations != null) annotations.AddRange(right.Annotations);
            result.Annotations = annotations;
        }

        if (left.AdditionalProperties != null || right.AdditionalProperties != null)
        {
            result.AdditionalProperties = new AdditionalPropertiesDictionary();
            if (left.AdditionalProperties != null)
            {
                foreach (var kvp in left.AdditionalProperties)
                {
                    result.AdditionalProperties[kvp.Key] = kvp.Value;
                }
            }

            if (right.AdditionalProperties != null)
            {
                foreach (var kvp in right.AdditionalProperties)
                {
                    result.AdditionalProperties[kvp.Key] = kvp.Value;
                }
            }
        }

        return result;
    }

    private static UsageDetails? CloneUsageDetails(UsageDetails? usage)
    {
        return usage == null
            ? null
            : new UsageDetails
            {
                InputTokenCount = usage.InputTokenCount,
                OutputTokenCount = usage.OutputTokenCount,
                TotalTokenCount = usage.TotalTokenCount,
            };
    }
}