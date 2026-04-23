using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Windows.ApplicationModel.Chat;
using LLMClient.Abstraction;
using LLMClient.ContextEngineering.PromptGeneration;
using LLMClient.Dialog.Models;
using Microsoft.Extensions.AI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using FunctionCallContent = Microsoft.Extensions.AI.FunctionCallContent;
using FunctionResultContent = Microsoft.Extensions.AI.FunctionResultContent;
using TextContent = Microsoft.Extensions.AI.TextContent;

namespace LLMClient.Agent;

/// <summary>
/// Prompt-based agent that prunes low-value rounds from a per-round message cache.
/// Extends <see cref="PromptBasedAgent"/> — the LLM invocation mechanics are handled by the base class.
/// <para>
/// The LLM decides which round indexes to remove; the class filters them out and returns the flat message list.
/// </para>
/// </summary>
public sealed class HistoryPruner : PromptBasedAgent
{
    /// <summary>Prompt template rendered with {{{task}}}, {{{contextHint}}}, {{{input}}} placeholders (Handlebars triple-brace for raw output).</summary>
    public string PromptTemplate { get; set; } = """
                                                 You are a precision history-pruning engine for an AI coding agent session.

                                                 # Task context

                                                 <task>
                                                   {{{task}}}
                                                 </task>

                                                 # Context hint 

                                                 <contextHint>
                                                    {{{contextHint}}}
                                                 </contextHint>

                                                 # History MessageRounds

                                                 Below are indexed message rounds:

                                                 <input>
                                                     {{{input}}}
                                                 </input>

                                                 Goal:
                                                 Remove only rounds that are irrelevant to the CURRENT coding objective.
                                                 Preserve all information that may be needed for further coding actions.

                                                 Hard KEEP rules (NEVER remove):
                                                 1) Any round containing code evidence:
                                                    - file/content reading results
                                                    - symbol/search results
                                                    - error logs/stack traces/test outputs
                                                    - diffs/patches/edits/command outputs used for coding decisions
                                                 2) Any round that defines or updates actionable plan/constraints/acceptance criteria.
                                                 3) Any round with inspection findings that impact implementation choices.
                                                 4) Any final conclusions, decisions, TODOs, or next-step instructions.
                                                 5) Any user requirement, explicit preference, or safety/format constraint.
                                                 6) If uncertain whether a round may be useful later, KEEP it.

                                                 Allowed REMOVE rules (only if clearly true):
                                                 A) Pure orchestration noise with no durable value:
                                                    - "thinking aloud" with no decision
                                                    - status chatter / progress filler
                                                    - repeated planner/inspector text that is fully subsumed by a later retained round
                                                 B) Tool-call wrappers that add no payload and no decision.
                                                 C) Duplicate rounds with near-identical content, keeping the most complete/latest one.

                                                 Safety constraints:
                                                 - Prefer under-deletion over over-deletion.
                                                 - Do NOT remove most rounds. If removal candidates exceed 40% of all rounds, keep only the highest-confidence candidates.
                                                 - Never remove all plan/inspect rounds if they contain decision context linked to coding.
                                                 - Never remove rounds required to understand why code changes were made.

                                                 Output format:
                                                 Return ONLY valid JSON, no prose:
                                                 {"removeIndexes":[...]}
                                                 Rules for output:
                                                 - Indexes must be unique integers in ascending order.
                                                 - If no high-confidence removals exist, return {"removeIndexes":[]}.
                                                 """;

    /// <summary>Tag used in error trace messages, e.g. "InspectCompact".</summary>
    public required string ErrorTag { get; init; }

    public HistoryPruner(ILLMChatClient chatClient) : base(chatClient)
    {
        Timeout = new Duration(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Prunes low-value rounds from <paramref name="rawHistory"/> via LLM decision
    /// and returns the filtered flat message list.
    /// Falls back to the full message set when pruning fails or is not applicable.
    /// </summary>
    public async Task<List<ChatMessage>?> CompactAsync(
        string? task,
        string? systemPrompt,
        IReadOnlyList<ChatMessage> rawHistory,
        CancellationToken cancellationToken)
    {
        var removeIndexes = await TryGetRemoveIndexesAsync(task, systemPrompt, rawHistory, cancellationToken);
        if (removeIndexes == null || removeIndexes.Any())
        {
            return null;
        }

        return rawHistory.Where((_, index) => !removeIndexes.Contains(index)).ToList();
    }

    private async Task<IReadOnlyList<int>?> TryGetRemoveIndexesAsync(
        string? task,
        string? systemPrompt,
        IReadOnlyList<ChatMessage> roundMessages,
        CancellationToken cancellationToken)
    {
        if (roundMessages.Count == 0) return null;

        var indexedInput = BuildIndexedInput(roundMessages);
        if (string.IsNullOrWhiteSpace(indexedInput)) return null;

        try
        {
            var message = await PromptTemplateRenderer.RenderHandlebarsAsync(PromptTemplate,
                new Dictionary<string, object?>
                {
                    { "task", task },
                    { "contextHint", systemPrompt },
                    { "input", indexedInput }
                });
            var contextBuilder =
                DefaultRequestContextBuilder.CreateFromHistory([
                    new RequestViewItem(message)
                    {
                        ResponseFormat = ChatResponseFormat.ForJsonSchema<RemoveDecision>(),
                    }
                ], systemPrompt: systemPrompt);
            var result = await SendRequestAsync(contextBuilder, cancellationToken);

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

    private static string BuildIndexedInput(IReadOnlyList<ChatMessage> rawMessages)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < rawMessages.Count; i++)
        {
            builder.AppendLine($"[{i}]");
            var chatMessage = rawMessages[i];
            var roundParts = new List<string>();
            // Append structured Contents (FunctionCallContent, FunctionResultContent, etc.)
            foreach (var content in chatMessage.Contents)
            {
                switch (content)
                {
                    case FunctionCallContent call:
                        var argsStr = call.Arguments != null
                            ? string.Join(", ", call.Arguments.Select(kv => $"{kv.Key}={kv.Value}"))
                            : string.Empty;
                        roundParts.Add($"[Tool Call: {call.Name}({argsStr})]");
                        break;
                    case FunctionResultContent result:
                        var resultStr = result.Exception != null
                            ? $"[Error: {result.Exception.Message}]"
                            : result.Result?.ToString() ?? string.Empty;
                        roundParts.Add($"[Result: {resultStr}]");
                        break;
                    case TextContent text:
                        // Already handled via message.Text, but include for explicit structured text
                        if (!string.IsNullOrWhiteSpace(text.Text) &&
                            !roundParts.Contains(text.Text))
                        {
                            roundParts.Add(text.Text);
                        }

                        break;
                    default:
                        // Include other content types as their string representation
                        /*var otherStr = content.ToString();
                        if (!string.IsNullOrWhiteSpace(otherStr))
                        {
                            roundParts.Add(otherStr);
                        }*/

                        break;
                }
            }

            var roundText = string.Join("\n", roundParts.Where(t => !string.IsNullOrWhiteSpace(t)));
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
        [JsonPropertyName("removeIndexes")] public List<int>? Indexes { get; set; }
    }
}