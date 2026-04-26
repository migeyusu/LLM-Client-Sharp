using System.Text;
using Elsa.Extensions;
using LLMClient.Abstraction;
using Microsoft.Extensions.AI;

namespace LLMClient.Endpoints;

public class AgentTaskResult : CallResult
{
    public static readonly AgentTaskResult Empty = new();

    /// <summary>
    /// 有效调用次数
    /// </summary>
    public override int ValidCallTimes { get; set; } = 0;

    private List<ChatMessage> MessageList { get; set; } = [];

    public override IEnumerable<ChatMessage> Messages
    {
        get => MessageList;
        set
        {
            if (value is List<ChatMessage> chatMessages)
            {
                MessageList = chatMessages;
            }
            else
            {
                MessageList = value.ToList();
            }
        }
    }

    public string? FirstTextResponse
    {
        get { return Messages?.FirstOrDefault()?.Text; }
    }

    public string? GetContentAsString()
    {
        return Messages?.Aggregate(string.Empty, (current, message) => current + message.Text);
    }

    public void Add(CallResult? right)
    {
        if (right is null || right == Empty) return;
        Latency = Latency == 0 ? right.Latency : Latency;
        Duration += right.Duration;
        Exception ??= right.Exception;
        FinishReason = right.FinishReason ?? FinishReason;
        ValidCallTimes += right.ValidCallTimes;
        Price = (Price ?? 0) + (right.Price ?? 0);
        if (this.Usage == null)
        {
            this.Usage = right.Usage;
        }
        else
        {
            this.Usage.Add(right.Usage ?? new UsageDetails());
        }

        MessageList.AddRange(right.Messages);
        if (right.Annotations != null)
        {
            if (Annotations == null)
            {
                Annotations = right.Annotations.ToList();
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

        ProtocolLog ??= new StringBuilder();
        ProtocolLog.Append(right.ProtocolLog);
    }
}