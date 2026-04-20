using System.Windows;
using LLMClient.Abstraction;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Proc;

public sealed class TaskSummaryChatHistoryCompressionStrategy : IChatHistoryCompressionStrategy
{
    private static readonly Duration CompressionTimeout = new(TimeSpan.FromSeconds(60));

    private readonly Summarizer _summarizer;

    public TaskSummaryChatHistoryCompressionStrategy(Summarizer summarizer)
    {
        _summarizer = summarizer;
    }

    public async Task CompressAsync(ChatHistoryCompressionContext context,
        CancellationToken cancellationToken = default)
    {

        var segmentation = ReactHistorySegmenter.Segment(context.ChatHistory, context.AgentId);
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
            _summarizer.ConversationHistorySummaryPrompt, CompressionTimeout, context.CurrentClient, cancellationToken);
        if (string.IsNullOrWhiteSpace(summary))
        {
            return;
        }

        var replacement = new List<ChatMessage>(segmentation.PreambleMessages)
        {
            CreateSummaryMessage(summary, context.AgentId)
        };
        replacement.AddRange(segmentation.Rounds.Skip(keepFromIndex).SelectMany(round => round.Messages));

        context.ChatHistory.Clear();
        context.ChatHistory.AddRange(replacement);
        context.CompressionApplied = true;
    }

    private static ChatMessage CreateSummaryMessage(string summary, string? agentId)
    {
        var message = new ChatMessage(ChatRole.Assistant, "[Compressed history summary]\n" + summary.Trim());
        ReactHistorySegmenter.TagMessage(message, ReactHistorySegmenter.CompressedSummaryRoundNumber,
            ReactHistoryMessageKind.Assistant, agentId);
        return message;
    }
}