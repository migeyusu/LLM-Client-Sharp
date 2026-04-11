using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using LLMClient.Abstraction;
using LLMClient.ContextEngineering.PromptGeneration;
using LLMClient.Dialog.Models;
using Microsoft.Extensions.AI;

namespace LLMClient.Agent;

/// <summary>
/// Prompt-based agent that prunes low-value rounds from a per-round message cache
/// and produces a compact handoff summary for downstream agents.
/// Extends <see cref="PromptBasedAgent"/> — the LLM invocation mechanics are handled by the base class.
/// <para>
/// Encapsulates all compaction concerns: prompt rendering, decision deserialization,
/// round filtering, and summary assembly.
/// </para>
/// </summary>
public sealed class HistoryCompactor : PromptBasedAgent
{
    /// <summary>Prompt template rendered with {{$task}}, {{$contextHint}}, {{$input}} placeholders.</summary>
    public required string PromptTemplate { get; init; }

    /// <summary>Separator text prepended to the LLM summary when kept messages are non-empty.</summary>
    public required string HandoffSeparator { get; init; }

    /// <summary>Completion flag that must appear at the end of the summary.</summary>
    public required string CompletionFlag { get; init; }

    /// <summary>Tag used in error trace messages, e.g. "InspectCompact".</summary>
    public required string ErrorTag { get; init; }

    public HistoryCompactor(ILLMChatClient chatClient) : base(chatClient)
    {
        Timeout = new Duration(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Compacts <paramref name="roundMessages"/> via LLM and returns the final visible message list.
    /// Falls back to the full message set when compaction fails or is not applicable.
    /// </summary>
    public async Task<IReadOnlyList<ChatMessage>> CompactAsync(
        string? task,
        string? systemPrompt,
        IReadOnlyList<IReadOnlyList<ChatMessage>> roundMessages,
        CancellationToken cancellationToken)
    {
        var decision = await TryGetDecisionAsync(task, systemPrompt, roundMessages, cancellationToken);
        return BuildMessages(roundMessages, decision);
    }

    private async Task<CompactDecision?> TryGetDecisionAsync(
        string? task,
        string? systemPrompt,
        IReadOnlyList<IReadOnlyList<ChatMessage>> roundMessages,
        CancellationToken cancellationToken)
    {
        if (roundMessages.Count == 0) return null;

        var indexedInput = BuildIndexedInput(roundMessages);
        if (string.IsNullOrWhiteSpace(indexedInput)) return null;

        try
        {
            var message = await PromptTemplateRenderer.RenderAsync(PromptTemplate,
                new Dictionary<string, object?>
                {
                    { "task", task },
                    { "contextHint", systemPrompt },
                    { "input", indexedInput }
                });

            var result = await SendRequestAsync(
                DefaultDialogContextBuilder.CreateFromHistory([new RequestViewItem(message)], systemPrompt),
                cancellationToken);

            var jsonResponse = result.FirstTextResponse;
            if (string.IsNullOrWhiteSpace(jsonResponse)) return null;

            var decision = Deserialize(jsonResponse);
            if (decision == null) return null;

            if (!string.IsNullOrWhiteSpace(decision.Summary))
                decision.Summary = EnsureCompletionFlag(decision.Summary.Trim());

            if (decision.RemoveIndexes != null)
            {
                decision.RemoveIndexes = decision.RemoveIndexes
                    .Distinct()
                    .Where(i => i >= 0 && i < roundMessages.Count)
                    .OrderBy(i => i)
                    .ToList();
            }

            return decision;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[{ErrorTag} Error]: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Applies <paramref name="decision"/> to produce the final visible message list.
    /// Removes rounds in <see cref="CompactDecision.RemoveIndexes"/>, then appends the summary.
    /// Falls back to all rounds when the filtered result would be empty.
    /// </summary>
    private IReadOnlyList<ChatMessage> BuildMessages(
        IReadOnlyList<IReadOnlyList<ChatMessage>> roundMessages,
        CompactDecision? decision)
    {
        IEnumerable<IReadOnlyList<ChatMessage>> kept = roundMessages;
        if (decision?.RemoveIndexes is { Count: > 0 } removeIndexes)
        {
            var removeSet = removeIndexes.ToHashSet();
            var filtered = roundMessages.Where((_, i) => !removeSet.Contains(i)).ToList();
            if (filtered.Count > 0)
                kept = filtered;
        }

        var messages = kept.SelectMany(m => m).ToList();
        if (!string.IsNullOrWhiteSpace(decision?.Summary))
        {
            var summaryText = messages.Count == 0
                ? decision.Summary
                : $"\n\n{HandoffSeparator}\n{decision.Summary}";
            messages.Add(new ChatMessage(ChatRole.Assistant, summaryText));
        }

        return messages;
    }

    private string EnsureCompletionFlag(string summary)
    {
        return summary.Contains(CompletionFlag, StringComparison.Ordinal)
            ? summary
            : $"{summary.TrimEnd()}\n\n{CompletionFlag}";
    }

    private static string BuildIndexedInput(IReadOnlyList<IReadOnlyList<ChatMessage>> roundMessages)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < roundMessages.Count; i++)
        {
            var roundText = string.Join("\n", roundMessages[i]
                .Select(m => m.Text)
                .Where(t => !string.IsNullOrWhiteSpace(t)));
            builder.AppendLine($"[{i}]");
            builder.AppendLine(string.IsNullOrWhiteSpace(roundText) ? "[NoContent]" : roundText);
            builder.AppendLine("---");
        }

        return builder.ToString().TrimEnd();
    }

    private static CompactDecision? Deserialize(string jsonResponse)
    {
        var json = ExtractJsonObject(jsonResponse);
        return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<CompactDecision>(json);
    }

    private static string? ExtractJsonObject(string? response)
    {
        if (string.IsNullOrWhiteSpace(response)) return null;
        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');
        return start < 0 || end <= start ? null : response[start..(end + 1)];
    }

    private sealed class CompactDecision
    {
        [JsonPropertyName("removeIndexes")]
        public List<int>? RemoveIndexes { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }
    }
}

