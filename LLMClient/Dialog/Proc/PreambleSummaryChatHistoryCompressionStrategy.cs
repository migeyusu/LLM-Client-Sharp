using System.Windows;
using LLMClient.Abstraction;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Proc;

/// <summary>
/// One-time pre-processing strategy that compresses preamble messages (previous task context)
/// before the ReAct loop starts. Preamble messages are those without round tags —
/// typically historical conversation from earlier tasks.
/// </summary>
public sealed class PreambleSummaryChatHistoryCompressionStrategy : IChatHistoryCompressionStrategy
{
    private static readonly Duration CompressionTimeout = new(TimeSpan.FromSeconds(60));

    private readonly Summarizer _summarizer;

    private ITokensCounter _tokensCounter;

    public PreambleSummaryChatHistoryCompressionStrategy(Summarizer summarizer, ITokensCounter tokensCounter)
    {
        _summarizer = summarizer;
        _tokensCounter = tokensCounter;
    }

    public async Task<bool> ShouldCompress(ChatHistoryCompressionContext context)
    {
        var options = context.Options;
        if (!options.PreambleCompression)
        {
            return false;
        }

        var segmentation = ReactHistorySegmenter.Segment(context.ChatHistory);
        var preamble = segmentation.PreambleMessages;

        // Need at least: system + some historical messages + current user message
        if (preamble.Count <= 2)
        {
            return false;
        }

        var startIndex = 0;
        while (startIndex < preamble.Count && preamble[startIndex].Role == ChatRole.System)
        {
            startIndex++;
        }

        var endIndex = preamble.Count - 1;
        if (endIndex >= startIndex && preamble[endIndex].Role == ChatRole.User)
        {
            endIndex--;
        }

        var historicalCount = endIndex - startIndex + 1;
        if (historicalCount <= 0)
        {
            return false;
        }

        var historicalMessages = preamble.Skip(startIndex).Take(historicalCount).ToArray();
        var estimatedTokens = await _tokensCounter.EstimateTokens(historicalMessages);
        var modelMaxContextSize = context.CurrentClient.Model.MaxContextSize;
        var threshold = options.PreambleTokenThresholdPercent * modelMaxContextSize;
        return estimatedTokens > threshold;
    }

    public async Task CompressAsync(ChatHistoryCompressionContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await ShouldCompress(context))
        {
            return;
        }

        var options = context.Options;

        var segmentation = ReactHistorySegmenter.Segment(context.ChatHistory);
        var preamble = segmentation.PreambleMessages;

        // Need at least: system + some historical messages + current user message
        if (preamble.Count <= 2)
        {
            return;
        }

        // Partition preamble into: leading system messages | historical messages | trailing user message
        var systemMessages = new List<ChatMessage>();
        int startIndex = 0;
        while (startIndex < preamble.Count && preamble[startIndex].Role == ChatRole.System)
        {
            systemMessages.Add(preamble[startIndex]);
            startIndex++;
        }

        // The last User message in preamble is the current task request — preserve it
        ChatMessage? currentUserMessage = null;
        int endIndex = preamble.Count - 1;
        if (endIndex >= startIndex && preamble[endIndex].Role == ChatRole.User)
        {
            currentUserMessage = preamble[endIndex];
            endIndex--;
        }

        // Everything between system messages and the current user message is historical context
        var historicalMessages = new List<ChatMessage>();
        for (int i = startIndex; i <= endIndex; i++)
        {
            historicalMessages.Add(preamble[i]);
        }

        if (historicalMessages.Count == 0)
        {
            return;
        }

        // Check if compression is needed based on token threshold
        var estimatedTokens = await _tokensCounter.EstimateTokens(historicalMessages);
        var modelMaxContextSize = context.CurrentClient.Model.MaxContextSize;
        var threshold = options.PreambleTokenThresholdPercent * modelMaxContextSize;
        if (estimatedTokens <= threshold)
        {
            return;
        }

        // Summarize historical messages
        context.Step?.EmitDiagnostic(DiagLevel.Info, $"Summarizing preamble context ({historicalMessages.Count} messages)...");
        var summary = await _summarizer.SummarizeChatMessagesAsync(
            historicalMessages, _summarizer.ConversationHistorySummaryPrompt, CompressionTimeout,
            context.CurrentClient, cancellationToken);

        if (string.IsNullOrWhiteSpace(summary))
        {
            context.Step?.EmitDiagnostic(DiagLevel.Warning, "Preamble summarization returned empty result, skipping compression.");
            return;
        }

        // Rebuild chat history: system + summary + current user + rounds
        var replacement = new List<ChatMessage>(systemMessages) { CreatePreambleSummaryMessage(summary) };
        if (currentUserMessage != null)
        {
            replacement.Add(currentUserMessage);
        }

        replacement.AddRange(segmentation.Rounds.SelectMany(round => round.Messages));

        context.ChatHistory.Clear();
        context.ChatHistory.AddRange(replacement);
        context.CompressionApplied = true;
    }

    private static ChatMessage CreatePreambleSummaryMessage(string summary)
    {
        return new ChatMessage(ChatRole.Assistant,
            "[Previous task context summary]\n" + summary.Trim());
    }
}