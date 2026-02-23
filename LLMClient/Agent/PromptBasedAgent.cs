using System.Text;
using System.Windows;
using LLMClient.Abstraction;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;

namespace LLMClient.Agent;

public class PromptBasedAgent : IAgent
{
    private readonly ILLMChatClient _chatClient;

    private readonly IInvokeInteractor? _interactor;

    public UsageDetails Usage { get; set; } = new();

    public double? Price { get; set; } = 0;

    public PromptBasedAgent(ILLMChatClient chatClient, IInvokeInteractor? interactor)
    {
        _chatClient = chatClient;
        _interactor = interactor;
    }

    public int RetryCount { get; set; } = 3;

    public Duration Timeout { get; set; } = Duration.Forever;

    public Task<CompletedResult> SendRequestAsync(ITextDialogSession session,
        CancellationToken cancellationToken = default)
    {
        var context = new DialogContext(session.DialogItems, session.SystemPrompt);
        return SendRequestAsync(context, cancellationToken);
    }

    public async Task<CompletedResult> SendRequestAsync(DialogContext context,
        CancellationToken cancellationToken = default)
    {
        CompletedResult? completedResult = null;
        var tryCount = 0;
        while (tryCount < RetryCount && !cancellationToken.IsCancellationRequested)
        {
            if (Timeout.HasTimeSpan)
            {
                using (var timeoutTokenSource = new CancellationTokenSource(Timeout.TimeSpan))
                {
                    using (var linkedTokenSource =
                           CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, cancellationToken))
                    {
                        try
                        {
                            completedResult = await _chatClient
                                .SendRequest(context, null, linkedTokenSource.Token)
                                .ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            continue;
                        }
                    }
                }
            }
            else
            {
                completedResult = await _chatClient.SendRequest(context, null, cancellationToken)
                    .ConfigureAwait(false);
            }

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

        var stringBuilder =
            new StringBuilder($"Failed to get a valid rsesponse from the LLM after {RetryCount} attempts.");
        if (completedResult?.ErrorMessage != null)
        {
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("Error Message:");
            stringBuilder.AppendLine(completedResult.ErrorMessage);
        }

        var s = stringBuilder.ToString();
        _interactor?.Error(s);
        throw new Exception(s);
    }

    public async Task<string> GetMessageAsync(string prompt, string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var context = new DialogContext([
            new RequestViewItem(prompt)
        ], systemPrompt);

        var sendRequestAsync = await SendRequestAsync(context, cancellationToken);
        return sendRequestAsync.GetContentAsString()!;
    }
}