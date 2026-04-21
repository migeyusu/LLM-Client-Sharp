using System.Text;
using System.Threading;
using System.Windows;
using LLMClient.Abstraction;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;

namespace LLMClient.Agent;

public class PromptBasedAgent
{
    private readonly ILLMChatClient _chatClient;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public UsageDetails Usage { get; set; } = new();

    public double? Price { get; set; } = 0;

    public PromptBasedAgent(ILLMChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public int RetryCount { get; set; } = 3;

    public Duration Timeout { get; set; } = Duration.Forever;

    public async Task<AgentTaskResult> SendRequestAsync(DefaultRequestContextBuilder contextBuilder,
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            AgentTaskResult? completedResult = null;
            var tryCount = 0;
            var requestContext = await contextBuilder.BuildAsync(_chatClient.Model, cancellationToken);
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
                                    .SendRequestCompatAsync(requestContext, linkedTokenSource.Token)
                                    .ConfigureAwait(false);
                                if (completedResult.IsInvalidRequest || completedResult.IsCanceled)
                                {
                                    throw completedResult.Exception;
                                }
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
                    completedResult = await _chatClient.SendRequestCompatAsync(requestContext, cancellationToken)
                        .ConfigureAwait(false);
                }

                tryCount++;
                if (completedResult.IsInterrupt)
                {
                    // Diagnostic logging is now handled via LoopEvent stream
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
                    // retry
                }
                else
                {
                    return completedResult;
                }
            }

            var stringBuilder =
                new StringBuilder($"Error:Failed to get a valid response from the LLM after {RetryCount} attempts.");
            if (completedResult?.ErrorMessage != null)
            {
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("Error Message:");
                stringBuilder.AppendLine(completedResult.ErrorMessage);
            }

            var s = stringBuilder.ToString();
            throw new Exception(s);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<string> GetMessageAsync(string prompt, string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var context = DefaultRequestContextBuilder.CreateFromHistory([
            new RequestViewItem(prompt)
        ], systemPrompt);
        var sendRequestAsync = await SendRequestAsync(context, cancellationToken);
        return sendRequestAsync.GetContentAsString()!;
    }
}