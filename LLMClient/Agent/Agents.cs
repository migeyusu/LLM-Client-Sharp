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

    public async Task<string> GetMessageAsync(string prompt, string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var context = new DialogContext([
            new RequestViewItem()
            {
                TextMessage = prompt
            }
        ], systemPrompt);
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

            var contentAsString = completedResult.GetContentAsString();
            if (string.IsNullOrEmpty(contentAsString))
            {
                _interactor?.Warning(
                    string.Format("The LLM returned an empty response. Retrying... (Attempt {0}/{1})",
                    tryCount, RetryCount));
            }
            else
            {
                return contentAsString;
            }
        }

        _interactor?.Error(string.Format("Failed to get a valid response from the LLM after {0} attempts.", RetryCount));
        throw new Exception("Failed to get a valid response from the LLM.");
    }
}