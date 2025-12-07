using LLMClient.Abstraction;
using LLMClient.Dialog;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace LLMClient.Agent;

public static class Agents
{
}

public interface IAgent
{
}

public class PromptAgent : IAgent
{
    private readonly ILLMChatClient _chatClient;

    private readonly IInvokeInteractor? _interactor;

    public UsageDetails Usage { get; set; } = new();

    public double? Price { get; set; } = 0;

    public PromptAgent(ILLMChatClient chatClient, IInvokeInteractor? interactor)
    {
        _chatClient = chatClient;
        _interactor = interactor;
    }

    public int RetryCount { get; set; } = 3;

    public Task<CompletedResult> SendRequestAsync(ITextDialogSession session,
        CancellationToken cancellationToken = default)
    {
        var context = new DialogContext(session.DialogItems, session.SystemPrompt);
        return SendRequestAsync(context, cancellationToken);
    }

    public async Task<CompletedResult> SendRequestAsync(DialogContext context,
        CancellationToken cancellationToken = default)
    {
        var tryCount = 0;
        while (tryCount < RetryCount)
        {
            var completedResult = await _chatClient.SendRequest(context, _interactor, cancellationToken)
                .ConfigureAwait(false);
            tryCount++;
            if (completedResult.IsInterrupt)
            {
                _interactor?.Warning(
                    string.Format("The LLM request was interrupted. Retrying... (Attempt {0}/{1})", tryCount,
                        RetryCount));
            }

            if (completedResult.Usage != null)
            {
                Usage.Add(completedResult.Usage);
            }

            if (completedResult.Price != null)
            {
                Price += completedResult.Price;
            }

            if (completedResult.IsInterrupt || string.IsNullOrEmpty(completedResult.GetContentAsString()))
            {
                _interactor?.Warning(
                    string.Format("The LLM returned an empty or interrupt response. Retrying... (Attempt {0}/{1})",
                        tryCount, RetryCount));
            }
            else
            {
                return completedResult;
            }
        }

        _interactor?.Error($"Failed to get a valid rsesponse from the LLM after {RetryCount} attempts.");
                                                              throw new Exception("Failed to get a valid reponse from the LLM.");
    }

    public async Task<string> GetMessageAsync(string prompt, string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var context = new DialogContext([
            new RequestViewItem()
            {
                TextMessage = prompt
            }
        ], systemPrompt);

        var sendRequestAsync = await SendRequestAsync(context, cancellationToken);
        return sendRequestAsync.GetContentAsString()!;
    }
}