using System.Text;
using System.Windows;
using LLMClient.Abstraction;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Proc;

internal static class ReactErrorRoundSummarizer
{
    private static readonly Duration ErrorSummaryTimeout = new(TimeSpan.FromSeconds(20));

    public static async Task<ChatMessage> BuildErrorSummaryMessageAsync(
        ReactHistoryRound round,
        Summarizer? summarizer,
        ILLMChatClient currentClient,
        string? agentId,
        CancellationToken cancellationToken = default)
    {
        var summaryText = await TryBuildLlmSummaryAsync(round, summarizer, currentClient, cancellationToken)
                          ?? BuildFallbackSummary(round);
        var summaryMessage = new ChatMessage(ChatRole.System,
            $"[Round {round.RoundNumber} error summary] {summaryText}");
        ChatMessageHierarchy.TagLoopLevel(summaryMessage, round.RoundNumber, ReactHistoryMessageKind.Observation, agentId);
        return summaryMessage;
    }

    private static async Task<string?> TryBuildLlmSummaryAsync(
        ReactHistoryRound round,
        Summarizer? summarizer,
        ILLMChatClient currentClient,
        CancellationToken cancellationToken)
    {
        if (summarizer == null)
        {
            return null;
        }

        var fallbackClient = currentClient is LlmClientBase
            ? null
            : currentClient;
        var llmSummary = await summarizer.SummarizeChatMessagesAsync(
            round.Messages.ToArray(),
            BuildPrompt(round.RoundNumber),
            ErrorSummaryTimeout,
            fallbackClient,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(llmSummary))
        {
            return null;
        }

        return Normalize(llmSummary);
    }

    private static string BuildPrompt(int roundNumber)
    {
        return $"Summarize failed ReAct round {roundNumber} in one short continuation sentence. " +
               "Use this exact pattern: What was done - What result. " +
               "Focus on the attempted action/tool and the key error outcome. " +
               "No bullets, no markdown, max 30 words.";
    }

    private static string BuildFallbackSummary(ReactHistoryRound round)
    {
        var action = GetActionSummary(round);
        var result = GetResultSummary(round);
        return $"What was done: {action} - What result: {result}";
    }

    private static string GetActionSummary(ReactHistoryRound round)
    {
        if (round.AssistantMessage == null)
        {
            return "attempted an action";
        }

        var toolCalls = round.AssistantMessage.Contents.OfType<FunctionCallContent>()
            .Select(content => content.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (toolCalls.Length > 0)
        {
            return $"called tool(s): {string.Join(", ", toolCalls)}";
        }

        var assistantText = string.Join(" ", round.AssistantMessage.Contents.OfType<TextContent>()
            .Select(content => content.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text!.Trim()));
        return string.IsNullOrWhiteSpace(assistantText)
            ? "attempted an action"
            : Trim(assistantText, 80);
    }

    private static string GetResultSummary(ReactHistoryRound round)
    {
        if (round.ObservationMessage == null)
        {
            return "received an error output";
        }

        var errorResults = round.ObservationMessage.Contents.OfType<FunctionResultContent>()
            .Where(content => content.Exception != null)
            .Select(content =>
            {
                var message = content.Exception?.Message;
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return message;
                }

                return content.Result?.ToString();
            })
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text!.Trim())
            .ToArray();
        if (errorResults.Length > 0)
        {
            return Trim(string.Join("; ", errorResults), 120);
        }

        var observationText = string.Join(" ", round.ObservationMessage.Contents.OfType<TextContent>()
            .Select(content => content.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text!.Trim()));
        return string.IsNullOrWhiteSpace(observationText)
            ? "received an error output"
            : Trim(observationText, 120);
    }

    private static string Normalize(string summary)
    {
        return string.Join(" ", summary
            .Split(["\r", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string Trim(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        var builder = new StringBuilder(value[..maxLength]);
        builder.Append("...");
        return builder.ToString();
    }
}

