﻿using LLMClient.Abstraction;
using LLMClient.Dialog;
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

    private readonly Action<string>? _stream;

    private readonly ILogger? _promptLogger;

    public UsageDetails Usage { get; set; } = new();

    public double? Price { get; set; } = 0;

    public PromptAgent(ILLMChatClient chatClient, Action<string>? stream, ILogger? promptLogger)
    {
        _chatClient = chatClient;
        _stream = stream;
        _promptLogger = promptLogger;
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
            var completedResult = await _chatClient.SendRequest(context, _stream, _promptLogger, cancellationToken)
                .ConfigureAwait(false);
            tryCount++;
            if (completedResult.IsInterrupt)
            {
                _promptLogger?.LogWarning(
                    "The LLM request was interrupted. Retrying... (Attempt {TryCount}/{RetryCount})",
                    tryCount, RetryCount);
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
                _promptLogger?.LogWarning(
                    "The LLM returned an empty response. Retrying... (Attempt {TryCount}/{RetryCount})",
                    tryCount, RetryCount);
            }
            else
            {
                return contentAsString;
            }
        }

        _promptLogger?.LogError("Failed to get a valid response from the LLM after {RetryCount} attempts.", RetryCount);
        throw new Exception("Failed to get a valid response from the LLM.");
    }
}