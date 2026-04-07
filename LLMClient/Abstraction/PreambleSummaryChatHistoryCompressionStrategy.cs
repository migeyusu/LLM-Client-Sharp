using System.Windows;
using LLMClient.Dialog;
using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

/// <summary>
/// One-time pre-processing strategy that compresses preamble messages (previous task context)
/// before the ReAct loop starts. Preamble messages are those without round tags —
/// typically historical conversation from earlier tasks.
/// </summary>
public sealed class PreambleSummaryChatHistoryCompressionStrategy : IChatHistoryCompressionStrategy
{
    private static readonly Duration CompressionTimeout = new(TimeSpan.FromSeconds(60));

    private const string PreambleSummaryPrompt =
        "Summarize the previous task context into a concise continuation note. " +
        "Focus on: the task goal and current progress, key decisions made, " +
        "files inspected or changed, important observations and failures, " +
        "and remaining work to be done. " +
        "Keep the result concise but complete enough to continue the task without the original messages.";

    private readonly Summarizer _summarizer;

    public PreambleSummaryChatHistoryCompressionStrategy(Summarizer summarizer)
    {
        _summarizer = summarizer;
    }

    public async Task CompressAsync(ChatHistoryCompressionContext context,
        CancellationToken cancellationToken = default)
    {
        var options = context.Options;
        if (!options.PreambleCompression)
        {
            return;
        }

        var segmentation = ReactHistorySegmenter.Segment(context.ChatHistory);
        var preamble = segmentation.PreambleMessages;

        // Need at least: system + some historical messages + current user message
        if (preamble.Count <= 2)
        {
            return;
        }

        // Partition preamble into: leading system messages | historical messages | trailing user message
        var systemMessages = new List<ChatMessage>();
        int startIndex = 0;
        while (startIndex < preamble.Count && preamble[startIndex].Role == ChatRole.System)
        {
            systemMessages.Add(preamble[startIndex]);
            startIndex++;
        }

        // The last User message in preamble is the current task request — preserve it
        ChatMessage? currentUserMessage = null;
        int endIndex = preamble.Count - 1;
        if (endIndex >= startIndex && preamble[endIndex].Role == ChatRole.User)
        {
            currentUserMessage = preamble[endIndex];
            endIndex--;
        }

        // Everything between system messages and the current user message is historical context
        var historicalMessages = new List<ChatMessage>();
        for (int i = startIndex; i <= endIndex; i++)
        {
            historicalMessages.Add(preamble[i]);
        }

        if (historicalMessages.Count == 0)
        {
            return;
        }

        // Check if compression is needed based on token threshold
        var estimatedTokens = EstimateTokens(historicalMessages);
        if (estimatedTokens <= options.PreambleTokenThreshold)
        {
            return;
        }

        // Summarize historical messages
        var summary = await _summarizer.SummarizeChatMessagesAsync(
            historicalMessages, PreambleSummaryPrompt, CompressionTimeout,
            context.CurrentClient, cancellationToken);

        if (string.IsNullOrWhiteSpace(summary))
        {
            return;
        }

        // Rebuild chat history: system + summary + current user + rounds
        var replacement = new List<ChatMessage>(systemMessages);
        replacement.Add(CreatePreambleSummaryMessage(summary));
        if (currentUserMessage != null)
        {
            replacement.Add(currentUserMessage);
        }

        replacement.AddRange(segmentation.Rounds.SelectMany(round => round.Messages));

        context.ChatHistory.Clear();
        context.ChatHistory.AddRange(replacement);
        context.CompressionApplied = true;
    }

    private static ChatMessage CreatePreambleSummaryMessage(string summary)
    {
        return new ChatMessage(ChatRole.Assistant,
            "[Previous task context summary]\n" + summary.Trim());
    }

    internal static long EstimateTokens(IReadOnlyList<ChatMessage> messages)
    {
        long totalChars = 0;
        foreach (var message in messages)
        {
            foreach (var content in message.Contents)
            {
                switch (content)
                {
                    case TextContent textContent:
                        totalChars += textContent.Text?.Length ?? 0;
                        break;
                    case TextReasoningContent reasoningContent:
                        totalChars += reasoningContent.Text.Length;
                        break;
                    case FunctionCallContent functionCallContent:
                        totalChars += functionCallContent.Name.Length;
                        if (functionCallContent.Arguments != null)
                        {
                            foreach (var arg in functionCallContent.Arguments)
                            {
                                totalChars += arg.Value?.ToString()?.Length ?? 0;
                            }
                        }

                        break;
                    case FunctionResultContent functionResultContent:
                        totalChars += functionResultContent.Result?.ToString()?.Length ?? 0;
                        break;
                }
            }
        }
        
        return (long)(totalChars / 2.8);
    }
}