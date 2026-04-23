using System.Text;
using System.Windows;
using LLMClient.Abstraction;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Proc;

/// <summary>
/// only summary error rounds, keep all other rounds. This is the most conservative strategy that can ensure the quality of the summary rounds,
/// but may not reduce the token count significantly if there are many non-error rounds.
/// </summary>
public sealed class ErrorSummaryChatHistoryCompressionStrategy : IChatHistoryCompressionStrategy
{
    private readonly Summarizer _summarizer;

    public ErrorSummaryChatHistoryCompressionStrategy(Summarizer summarizer)
    {
        _summarizer = summarizer;
    }

    public Task CompressAsync(ChatHistoryContext context, CancellationToken cancellationToken = default)
    {
        return context.History.TryApplyCompressItems(context.Options.PreserveRecentRounds, async round =>
        {
            if (round.IsErrorRound)
            {
                var errorSummaryMessage = await ReactErrorRoundSummarizer.BuildErrorSummaryMessageAsync(
                    round, _summarizer, context.CurrentClient, cancellationToken);
                return new ReactRound()
                {
                    RoundNumber = round.RoundNumber,
                    IsCompressApplied = false,
                    AssistantMessage = errorSummaryMessage
                };
            }

            return round;
        });
    }
}

public sealed class LoopSummaryChatHistoryCompressionStrategy : IChatHistoryCompressionStrategy
{
    private static readonly Duration CompressionTimeout = new(TimeSpan.FromSeconds(30));

    private readonly Summarizer _summarizer;

    public LoopSummaryChatHistoryCompressionStrategy(Summarizer summarizer)
    {
        _summarizer = summarizer;
    }

    public Task CompressAsync(ChatHistoryContext context,
        CancellationToken cancellationToken = default)
    {
        return context.History.TryApplyCompressItems(context.Options.PreserveRecentRounds, async round =>
        {
            var summaryText = await BuildRoundSummaryAsync(round, context.CurrentClient, cancellationToken);
            var assistantMessage = round.AssistantMessage?.Clone();
            if (assistantMessage == null)
            {
                assistantMessage = new ChatMessage(ChatRole.Assistant, summaryText);
            }
            else
            {
                assistantMessage.Contents = new List<AIContent>() { new TextContent(summaryText) };
            }

            return new ReactRound()
            {
                RoundNumber = round.RoundNumber,
                IsCompressApplied = true,
                AssistantMessage = assistantMessage,
            };
        });
    }

    private async Task<string> BuildRoundSummaryAsync(
        ReactRound round,
        ILLMChatClient currentClient,
        CancellationToken cancellationToken)
    {
        var roundMessages = round.Messages.ToArray();
        var prompt = BuildRoundSummaryPrompt(round.RoundNumber);
        var llmSummary = await _summarizer.SummarizeChatMessagesAsync(roundMessages,
            prompt, CompressionTimeout, currentClient, cancellationToken);
        if (!string.IsNullOrWhiteSpace(llmSummary))
        {
            return $"[Round {round.RoundNumber} summary] {NormalizeSummary(llmSummary)}";
        }

        return BuildFallbackRoundSummary(round);
    }

    private static string BuildRoundSummaryPrompt(int roundNumber)
    {
        return $"Summarize ReAct round {roundNumber} into one very short continuation note. " +
               "Include only the essential reasoning, the action taken, and the important observation or tool result. " +
               "Do not mention formatting instructions, do not use bullets, do not say 'the assistant', and do not exceed 40 words.";
    }

    private static string NormalizeSummary(string summary)
    {
        return string.Join(" ", summary
            .Split(["\r", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string BuildFallbackRoundSummary(ReactRound round)
    {
        var reasoning = GetContent<TextReasoningContent>(round.AssistantMessage, content => content.Text);
        var text = GetContent<TextContent>(round.AssistantMessage, content => content.Text);
        var toolNames = round.AssistantMessage?.Contents.OfType<FunctionCallContent>()
            .Select(content => content.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        var observations = GetObservationSummary(round);

        var builder = new StringBuilder();
        builder.Append($"[Round {round.RoundNumber} summary] ");
        if (!string.IsNullOrWhiteSpace(reasoning))
        {
            builder.Append("Reasoning: ");
            builder.Append(Trim(reasoning));
            builder.Append(". ");
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            builder.Append("Action: ");
            builder.Append(Trim(text));
            builder.Append(". ");
        }

        if (toolNames.Length > 0)
        {
            builder.Append("Tools used: ");
            builder.Append(string.Join(", ", toolNames));
            builder.Append(". ");
        }

        if (!string.IsNullOrWhiteSpace(observations))
        {
            builder.Append("Observation: ");
            builder.Append(Trim(observations));
            builder.Append('.');
        }

        return builder.ToString().Trim();
    }

    private static string? GetObservationSummary(ReactRound round)
    {
        if (round.ObservationMessage == null)
        {
            return null;
        }

        var functionResults = round.ObservationMessage.Contents.OfType<FunctionResultContent>()
            .Select(result => result.Result switch
            {
                null => null,
                string textVal => textVal,
                _ => result.Result?.ToString(),
            })
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text!.Trim())
            .ToArray();
        if (functionResults.Length > 0)
        {
            return string.Join("; ", functionResults);
        }

        return GetContent<TextContent>(round.ObservationMessage, content => content.Text);
    }

    private static string? GetContent<TContent>(
        ChatMessage? message,
        Func<TContent, string?> selector)
        where TContent : AIContent
    {
        if (message == null)
        {
            return null;
        }

        var values = message.Contents.OfType<TContent>()
            .Select(selector)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text!.Trim())
            .ToArray();
        if (values.Length == 0)
        {
            return null;
        }

        return string.Join(" ", values);
    }

    private static string Trim(string text)
    {
        const int maxLength = 160;
        return text.Length <= maxLength
            ? text
            : text[..maxLength] + "...";
    }
}