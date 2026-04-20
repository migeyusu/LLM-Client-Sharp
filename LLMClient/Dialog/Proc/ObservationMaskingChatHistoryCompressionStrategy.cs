using LLMClient.Abstraction;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Proc;

public sealed class ObservationMaskingChatHistoryCompressionStrategy : IChatHistoryCompressionStrategy
{
    public async Task CompressAsync(ChatHistoryCompressionContext context, CancellationToken cancellationToken = default)
    {
        var segmentation = ReactHistorySegmenter.Segment(context.ChatHistory, context.AgentId);
        var roundsToKeep = Math.Max(0, context.Options.PreserveRecentRounds);
        var keepFromIndex = Math.Max(0, segmentation.Rounds.Count - roundsToKeep);

        var hasObservationsToMask = segmentation.Rounds.Take(keepFromIndex).Any(round => round.ObservationMessages.Count > 0);
        if (!hasObservationsToMask)
        {
            return;
        }

        var replacement = new List<ChatMessage>(segmentation.PreambleMessages);
        var changed = false;

        for (var index = 0; index < segmentation.Rounds.Count; index++)
        {
            var round = segmentation.Rounds[index];

            replacement.AddRange(round.AssistantMessages);
            if (index >= keepFromIndex)
            {
                replacement.AddRange(round.ObservationMessages);
                continue;
            }

            foreach (var observationMessage in round.ObservationMessages)
            {
                replacement.Add(CreateObservationPlaceholder(observationMessage, round.RoundNumber,
                    context.Options.ObservationPlaceholder, context.AgentId));
            }

            if (round.ObservationMessages.Count > 0)
            {
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        context.ChatHistory.Clear();
        context.ChatHistory.AddRange(replacement);
        context.CompressionApplied = true;
    }

    private static ChatMessage CreateObservationPlaceholder(ChatMessage originalMessage, int roundNumber, string placeholder, string? agentId)
    {
        ChatMessage placeholderMessage;
        var contents = originalMessage.Contents;
        if (contents.OfType<FunctionResultContent>().Any())
        {
            placeholderMessage = new ChatMessage
            {
                Role = originalMessage.Role,
            };
            foreach (var result in contents.OfType<FunctionResultContent>())
            {
                placeholderMessage.Contents.Add(new FunctionResultContent(result.CallId, placeholder));
            }
        }
        else
        {
            placeholderMessage = new ChatMessage(originalMessage.Role, placeholder);
        }

        ReactHistorySegmenter.TagMessage(placeholderMessage, roundNumber, ReactHistoryMessageKind.Observation, agentId);
        return placeholderMessage;
    }
}
