using LLMClient.Endpoints;
using Microsoft.Extensions.AI;
using System.Diagnostics;
using ChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace LLMClient.Abstraction;

public static class ChatEndpointExtensions
{
    /// <summary>
    /// 兼容层：将 IAsyncEnumerable&lt;ReactStep&gt; 聚合为单一 ChatCallResult。
    /// 用于所有仍需 ChatCallResult 的消费者。
    /// </summary>
    public static async Task<ChatCallResult> SendRequestCompatAsync(
        this IChatEndpoint endpoint,
        RequestContext context,
        CancellationToken cancellationToken = default)
    {
        var totalUsage = new UsageDetails
        {
            InputTokenCount = 0,
            OutputTokenCount = 0,
            TotalTokenCount = 0,
        };
        var allMessages = new List<ChatMessage>();
        ChatFinishReason? finishReason = null;
        Exception? exception = null;
        int totalLatency = 0;
        int validCallTimes = 0;
        UsageDetails? lastSuccessfulUsage = null;
        var sw = Stopwatch.StartNew();

        await foreach (var step in endpoint.SendRequestAsync(context, cancellationToken))
        {
            // 必须消费完内层事件流，否则 step.Result 不可用
            await foreach (var _ in step.WithCancellation(cancellationToken))
            {
            }

            var result = step.Result;
            if (result == null) continue;

            if (result.Usage != null)
            {
                totalUsage.Add(result.Usage);
                lastSuccessfulUsage = CloneUsageDetails(result.Usage);
            }

            allMessages.AddRange(result.Messages);
            finishReason = result.FinishReason;
            exception ??= result.Exception;
            totalLatency += result.LatencyMs;

            if (result.Exception == null)
                validCallTimes++;
        }

        return new ChatCallResult
        {
            Usage = totalUsage,
            LastSuccessfulUsage = lastSuccessfulUsage,
            Messages = allMessages,
            FinishReason = finishReason,
            Exception = exception,
            Latency = totalLatency,
            Duration = (int)Math.Ceiling(sw.ElapsedMilliseconds / 1000f),
            ValidCallTimes = validCallTimes,
        };
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

