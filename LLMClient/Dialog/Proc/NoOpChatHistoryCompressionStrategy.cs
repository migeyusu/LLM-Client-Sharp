using LLMClient.Abstraction;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Proc;

public sealed class NoOpChatHistoryCompressionStrategy : IChatHistoryCompressionStrategy
{
    private readonly Summarizer? _summarizer;

    public NoOpChatHistoryCompressionStrategy(Summarizer? summarizer = null)
    {
        _summarizer = summarizer;
    }

    public bool ShouldCompress(ChatHistoryCompressionContext context)
    {
        if (!context.Options.SummaryErrorLoop)
        {
            return false;
        }

        var segmentation = ReactHistorySegmenter.Segment(context.ChatHistory);
        var roundsToKeep = Math.Max(0, context.Options.PreserveRecentRounds);
        if (segmentation.Rounds.Count <= roundsToKeep)
        {
            return false;
        }

        var keepFromIndex = Math.Max(0, segmentation.Rounds.Count - roundsToKeep);
        return segmentation.Rounds.Take(keepFromIndex).Any(round => round.HasError);
    }

    public async Task CompressAsync(ChatHistoryCompressionContext context, CancellationToken cancellationToken = default)
    {
        if (!ShouldCompress(context))
        {
            return;
        }

        var segmentation = ReactHistorySegmenter.Segment(context.ChatHistory);
        var roundsToKeep = Math.Max(0, context.Options.PreserveRecentRounds);
        var keepFromIndex = Math.Max(0, segmentation.Rounds.Count - roundsToKeep);
        var replacement = new List<ChatMessage>();
        var changed = false;

        for (var index = 0; index < segmentation.Rounds.Count; index++)
        {
            var round = segmentation.Rounds[index];
            if (round.HasError && index < keepFromIndex)
            {
                replacement.Add(await ReactErrorRoundSummarizer.BuildErrorSummaryMessageAsync(
                    round,
                    _summarizer,
                    context.CurrentClient,
                    cancellationToken));
                changed = true;
                continue;
            }

            replacement.AddRange(round.AssistantMessages);
            replacement.AddRange(round.ObservationMessages);
        }

        if (!changed)
        {
            return;
        }

        context.ChatHistory.Clear();
        context.ChatHistory.AddRange(replacement);
        context.CompressionApplied = true;
    }
}
