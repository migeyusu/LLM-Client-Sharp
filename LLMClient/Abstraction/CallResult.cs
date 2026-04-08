using System.Diagnostics.CodeAnalysis;
using LLMClient.Endpoints;
using LLMClient.Endpoints.Messages;
using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public class CallResult : IResponse
{
    public UsageDetails? Usage { get; set; }

    /// <summary>
    /// 最后一次成功调用返回的 usage，不参与多轮累计。
    /// </summary>
    public UsageDetails? LastSuccessfulUsage { get; set; }

    public ChatFinishReason? FinishReason { get; set; }

    public long Tokens
    {
        get { return Usage?.OutputTokenCount ?? 0; }
    }

    public int Duration { get; set; }

    public string? ErrorMessage
    {
        get
        {
            if (Exception == null)
            {
                return null;
            }

            if (field == null)
            {
                field = Exception.HierarchicalMessage();
            }

            return field;
        }
    }

    public IEnumerable<ChatMessage> Messages { get; set; } = [];

    public int Latency { get; set; }

    public double? Price { get; set; }

    public virtual int ValidCallTimes
    {
        get { return this.Exception == null ? 1 : 0; }
        set { throw new NotSupportedException(); }
    }

    public Exception? Exception { get; set; }

    public bool IsInterrupt
    {
        get { return Exception != null; }
    }

    [MemberNotNullWhen(true, nameof(Exception))]
    public bool IsCanceled => Exception is OperationCanceledException;

    [MemberNotNullWhen(true, nameof(Exception))]
    public bool IsInvalidRequest => Exception is LlmInvalidRequestException;

    public bool IsUnhandledError
    {
        get { return Exception is ChatCriticalException; }
    }

    public IList<ChatAnnotation>? Annotations { get; set; }

    public AdditionalPropertiesDictionary? AdditionalProperties { get; set; }

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
}