using System.Text;
using System.Windows;
using LLMClient.Abstraction;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Proc;

public sealed class InfoCleaningChatHistoryCompressionStrategy : IChatHistoryCompressionStrategy
{
    private static readonly Duration CompressionTimeout = new(TimeSpan.FromSeconds(30));

    private readonly Summarizer _summarizer;

    public InfoCleaningChatHistoryCompressionStrategy(Summarizer summarizer)
    {
        _summarizer = summarizer;
    }

    public async Task CompressAsync(ChatHistoryCompressionContext context, CancellationToken cancellationToken = default)
    {

        var segmentation = ChatMessageHierarchy.SegmentReactLevel(context.ChatHistory, context.AgentId);
        var roundsToKeep = Math.Max(0, context.Options.PreserveRecentRounds);
        if (segmentation.Rounds.Count <= roundsToKeep)
        {
            return;
        }

        var replacement = new List<ChatMessage>(segmentation.PreambleMessages);
        var keepFromIndex = Math.Max(0, segmentation.Rounds.Count - roundsToKeep);

        for (var index = 0; index < segmentation.Rounds.Count; index++)
        {
            var round = segmentation.Rounds[index];
            if (index >= keepFromIndex)
            {
                if (round.AssistantMessage != null)
                {
                    replacement.Add(round.AssistantMessage);
                }

                if (round.ObservationMessage != null)
                {
                    replacement.Add(round.ObservationMessage);
                }

                continue;
            }

            var summaryText = await BuildRoundSummaryAsync(round, context.CurrentClient, cancellationToken);
            if (!string.IsNullOrWhiteSpace(summaryText))
            {
                var summaryMessage = new ChatMessage(ChatRole.Assistant, summaryText);
                ChatMessageHierarchy.TagLoopLevel(summaryMessage, round.RoundNumber, ReactHistoryMessageKind.Assistant, context.AgentId);
                replacement.Add(summaryMessage);
            }
        }

        context.ChatHistory.Clear();
        context.ChatHistory.AddRange(replacement);
        context.CompressionApplied = true;
    }

    private async Task<string> BuildRoundSummaryAsync(
        ReactHistoryRound round,
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

    private static string BuildFallbackRoundSummary(ReactHistoryRound round)
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

    private static string? GetObservationSummary(ReactHistoryRound round)
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
