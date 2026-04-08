using System.Text;
using Elsa.Extensions;
using LLMClient.Abstraction;
using LLMClient.Endpoints.Messages;
using Microsoft.Extensions.AI;

namespace LLMClient.Endpoints;

public class AgentTaskResult : CallResult
{
    public static readonly AgentTaskResult Empty = new();

    /// <summary>
    /// 有效调用次数
    /// </summary>
    public override int ValidCallTimes { get; set; } = 0;

    public string? FirstTextResponse
    {
        get { return Messages?.FirstOrDefault()?.Text; }
    }
    
    public StringBuilder History { get; } = new();

    public string? GetContentAsString()
    {
        return Messages?.Aggregate(string.Empty, (current, message) => current + message.Text);
    }

    public void Add(CallResult? right)
    {
        if (right is null || right == Empty) return;

        var usage = Usage != null || right.Usage != null
            ? new UsageDetails
            {
                InputTokenCount = (Usage?.InputTokenCount ?? 0) + (right.Usage?.InputTokenCount ?? 0),
                OutputTokenCount = (Usage?.OutputTokenCount ?? 0) + (right.Usage?.OutputTokenCount ?? 0),
                TotalTokenCount = (Usage?.TotalTokenCount ?? 0) + (right.Usage?.TotalTokenCount ?? 0),
            }
            : null;

        LastSuccessfulUsage = right.ValidCallTimes > 0
            ? CloneUsageDetails(right.LastSuccessfulUsage)
            : CloneUsageDetails(LastSuccessfulUsage);
        Latency = Latency == 0 ? right.Latency : Latency;
        Duration += right.Duration;
        Exception ??= right.Exception;
        FinishReason = right.FinishReason ?? FinishReason;
        ValidCallTimes += right.ValidCallTimes;
        Price = (Price ?? 0) + (right.Price ?? 0);
        Usage = usage;
        Messages = Messages.Concat(right.Messages).ToList();

        if (right.Annotations != null)
        {
            if (Annotations == null)
            {
                Annotations = new List<ChatAnnotation>(right.Annotations);
            }
            else
            {
                Annotations.AddRange(right.Annotations);
            }
        }

        if (right.AdditionalProperties != null)
        {
            AdditionalProperties ??= new AdditionalPropertiesDictionary();
            foreach (var kvp in right.AdditionalProperties)
            {
                AdditionalProperties[kvp.Key] = kvp.Value;
            }
        }
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