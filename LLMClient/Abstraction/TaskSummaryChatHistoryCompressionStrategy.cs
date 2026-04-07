using System.Windows;
using LLMClient.Dialog;
using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public sealed class TaskSummaryChatHistoryCompressionStrategy : IChatHistoryCompressionStrategy
{
    private static readonly Duration CompressionTimeout = new(TimeSpan.FromSeconds(60));

    private const string CompressionPrompt =
        "Summarize the earlier completed ReAct rounds for continued execution. Focus on the task goal, key decisions, tools used, files inspected or changed, important observations, failures, and the current plan. Do not repeat the most recent active rounds. Keep the result concise but complete enough for the next loop.";

    private readonly Summarizer _summarizer;

    public TaskSummaryChatHistoryCompressionStrategy(Summarizer summarizer)
    {
        _summarizer = summarizer;
    }

    public async Task CompressAsync(ChatHistoryCompressionContext context, CancellationToken cancellationToken = default)
    {
        var segmentation = ReactHistorySegmenter.Segment(context.ChatHistory);
        var roundsToKeep = Math.Max(0, context.Options.PreserveRecentRounds);
        if (segmentation.Rounds.Count <= roundsToKeep)
        {
            return;
        }

        var keepFromIndex = Math.Max(0, segmentation.Rounds.Count - roundsToKeep);
        var roundsToCompress = segmentation.Rounds.Take(keepFromIndex)
            .SelectMany(round => round.Messages)
            .ToArray();
        if (roundsToCompress.Length == 0)
        {
            return;
        }

        var summary = await _summarizer.SummarizeChatMessagesAsync(roundsToCompress,
            CompressionPrompt, CompressionTimeout, context.CurrentClient, cancellationToken);
        if (string.IsNullOrWhiteSpace(summary))
        {
            return;
        }

        var replacement = new List<ChatMessage>(segmentation.PreambleMessages)
        {
            CreateSummaryMessage(summary)
        };
        replacement.AddRange(segmentation.Rounds.Skip(keepFromIndex).SelectMany(round => round.Messages));

        context.ChatHistory.Clear();
        context.ChatHistory.AddRange(replacement);
        context.CompressionApplied = true;
    }

    private static ChatMessage CreateSummaryMessage(string summary)
    {
        var message = new ChatMessage(ChatRole.Assistant, "[Compressed history summary]\n" + summary.Trim());
        ReactHistorySegmenter.TagMessage(message, ReactHistorySegmenter.CompressedSummaryRoundNumber,
            ReactHistoryMessageKind.Assistant);
        return message;
    }
}

