using System.Windows;
using LLMClient.Abstraction;
using LLMClient.Dialog.Models;
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

    public async Task CompressAsync(ChatHistoryContext context,
        CancellationToken cancellationToken = default)
    {
        var segmentation = context.History;
        var roundsToKeep = context.Options.PreserveRecentRounds;
        var rounds = segmentation.Rounds;
        if (rounds.Count <= roundsToKeep)
        {
            return;
        }

        //每次總結總是放在最前
        var keepFromLength = rounds.Count - roundsToKeep;
        var roundsToCompress = rounds.Take(keepFromLength).ToArray();
        var rawRounds = roundsToCompress.Where(round => !round.IsCompressApplied).ToArray();
        if (rawRounds.Length == 0)
        {
            return;
        }

        //todo:
        var existSummary =
            roundsToCompress.LastOrDefault(round => round.IsCompressApplied)?.AssistantMessage?.Text;

        //summary additional message
        var summary = await _summarizer.SummarizeChatMessagesAsync(
            rawRounds.SelectMany(round => round.Messages).ToArray(),
            _summarizer.ConversationHistorySummaryPrompt, CompressionTimeout, context.CurrentClient, cancellationToken);
        if (string.IsNullOrWhiteSpace(summary))
        {
            return;
        }

        foreach (var roundToCompress in roundsToCompress)
        {
            roundToCompress.IsCompressApplied = true;
        }

        rounds.RemoveRange(0, keepFromLength);
        rounds.Insert(0, new ReactRound()
        {
            RoundNumber = 0,
            IsCompressApplied = true,
            AssistantMessage = CreateSummaryMessage(summary, 0)
        });
    }

    private static ChatMessage CreateSummaryMessage(string summary, int loopNumber)
    {
        var message = new ChatMessage(ChatRole.Assistant, "[Compressed history summary]\n" + summary.Trim());
        message.TagLoopLevel(loopNumber,
            ReactHistoryMessageKind.Assistant);
        return message;
    }
}