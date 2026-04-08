using LLMClient.Abstraction;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Proc;

public sealed class NoOpChatHistoryCompressionStrategy : IChatHistoryCompressionStrategy
{
    public static NoOpChatHistoryCompressionStrategy Instance { get; } = new();

    private NoOpChatHistoryCompressionStrategy()
    {
    }

    public Task CompressAsync(ChatHistoryCompressionContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Options.SummaryErrorLoop)
        {
            return Task.CompletedTask;
        }

        var segmentation = ReactHistorySegmenter.Segment(context.ChatHistory);
        if (segmentation.Rounds.Count == 0)
        {
            return Task.CompletedTask;
        }

        var replacement = new List<ChatMessage>(segmentation.PreambleMessages);
        var changed = false;

        for (var index = 0; index < segmentation.Rounds.Count; index++)
        {
            var round = segmentation.Rounds[index];
            if (round.HasError && index < segmentation.Rounds.Count - 1)
            {
                var summaryMsg = new ChatMessage(ChatRole.System, "[A previous erroneous action and its output have been omitted for brevity]");
                ReactHistorySegmenter.TagMessage(summaryMsg, round.RoundNumber, ReactHistoryMessageKind.Observation);
                replacement.Add(summaryMsg);
                changed = true;
            }
            else
            {
                replacement.AddRange(round.AssistantMessages);
                replacement.AddRange(round.ObservationMessages);
            }
        }

        if (changed)
        {
            context.ChatHistory.Clear();
            context.ChatHistory.AddRange(replacement);
            context.CompressionApplied = true;
        }

        return Task.CompletedTask;
    }
}
