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
/// Prompt-based agent that prunes low-value rounds from a per-round message cache.
/// Extends <see cref="PromptBasedAgent"/> — the LLM invocation mechanics are handled by the base class.
/// <para>
/// The LLM decides which round indexes to remove; the class filters them out and returns the flat message list.
/// </para>
/// </summary>
public sealed class HistoryCompactor : PromptBasedAgent
{
    /// <summary>Prompt template rendered with {{$task}}, {{$contextHint}}, {{$input}} placeholders.</summary>
    public required string PromptTemplate { get; init; }

    /// <summary>Tag used in error trace messages, e.g. "InspectCompact".</summary>
    public required string ErrorTag { get; init; }

    public HistoryCompactor(ILLMChatClient chatClient) : base(chatClient)
    {
        Timeout = new Duration(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Prunes low-value rounds from <paramref name="roundMessages"/> via LLM decision
    /// and returns the filtered flat message list.
    /// Falls back to the full message set when pruning fails or is not applicable.
    /// </summary>
    public async Task<IReadOnlyList<ChatMessage>> CompactAsync(
        string? task,
        string? systemPrompt,
        IReadOnlyList<IReadOnlyList<ChatMessage>> roundMessages,
        CancellationToken cancellationToken)
    {
        var removeIndexes = await TryGetRemoveIndexesAsync(task, systemPrompt, roundMessages, cancellationToken);
        return BuildMessages(roundMessages, removeIndexes);
    }

    private async Task<IReadOnlyList<int>?> TryGetRemoveIndexesAsync(
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

            var indexes = Deserialize(jsonResponse);
            if (indexes is not { Count: > 0 }) return null;

            return indexes
                .Distinct()
                .Where(i => i >= 0 && i < roundMessages.Count)
                .OrderBy(i => i)
                .ToList();
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
    /// Removes the rounds at <paramref name="removeIndexes"/> and returns the remaining messages as a flat list.
    /// Falls back to all rounds when the filtered result would be empty.
    /// </summary>
    private static IReadOnlyList<ChatMessage> BuildMessages(
        IReadOnlyList<IReadOnlyList<ChatMessage>> roundMessages,
        IReadOnlyList<int>? removeIndexes)
    {
        if (removeIndexes is not { Count: > 0 })
            return roundMessages.SelectMany(m => m).ToList();

        var removeSet = removeIndexes.ToHashSet();
        var filtered = roundMessages.Where((_, i) => !removeSet.Contains(i)).ToList();
        var kept = filtered.Count > 0
            ? (IEnumerable<IReadOnlyList<ChatMessage>>)filtered
            : roundMessages;
        return kept.SelectMany(m => m).ToList();
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

    private static List<int>? Deserialize(string jsonResponse)
    {
        var json = ExtractJsonObject(jsonResponse);
        if (string.IsNullOrWhiteSpace(json)) return null;
        var decision = JsonSerializer.Deserialize<RemoveDecision>(json);
        return decision?.Indexes;
    }

    private static string? ExtractJsonObject(string? response)
    {
        if (string.IsNullOrWhiteSpace(response)) return null;
        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');
        return start < 0 || end <= start ? null : response[start..(end + 1)];
    }

    private sealed class RemoveDecision
    {
        [JsonPropertyName("removeIndexes")]
        public List<int>? Indexes { get; set; }
    }
}
