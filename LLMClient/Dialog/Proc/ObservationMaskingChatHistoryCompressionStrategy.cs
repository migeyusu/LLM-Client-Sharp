using LLMClient.Abstraction;
using LLMClient.Dialog.Models;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Proc;

public sealed class ObservationMaskingChatHistoryCompressionStrategy : IChatHistoryCompressionStrategy
{
    public Task CompressAsync(ChatHistoryContext context,
        CancellationToken cancellationToken = default)
    {
        return context.History.TryApplyCompressItems(context.Options.PreserveRecentRounds, round =>
        {
            var aiContents = new List<AIContent>();
            ChatMessage observationMessage;
            if (round.ObservationMessage != null)
            {
                foreach (var aiContent in round.ObservationMessage.Contents)
                {
                    if (aiContent is FunctionResultContent functionResultContent)
                    {
                        aiContents.Add(new FunctionResultContent(functionResultContent.CallId,
                            context.Options.ObservationPlaceholder)
                        {
                            Exception = functionResultContent.Exception
                        });
                    }
                    else
                    {
                        aiContents.Add(aiContent);
                    }
                }

                observationMessage = round.ObservationMessage.Clone();
                observationMessage.Contents = aiContents;
            }
            else
            {
                observationMessage = new ChatMessage(ChatRole.Tool, context.Options.ObservationPlaceholder);
            }

            var reactRound = new ReactRound()
            {
                RoundNumber = round.RoundNumber,
                AssistantMessage = round.AssistantMessage?.Clone(),
                ObservationMessage = observationMessage
            };

            return Task.FromResult(reactRound);
        });
    }
}