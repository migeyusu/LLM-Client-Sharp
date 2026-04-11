using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Agent.MiniSWE;
using LLMClient.ContextEngineering.PromptGeneration;
using LLMClient.ContextEngineering.Tools;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.Project;
using LLMClient.ToolCall;
using LLMClient.ToolCall.DefaultPlugins;
using Microsoft.Extensions.AI;

namespace LLMClient.Agent;

/// <summary>
/// Read-only agent base class that wraps each ReAct round's <see cref="ReactStep"/>,
/// suppresses intermediate <see cref="CallResult.Messages"/> (chat history),
/// caches them per round, and at task completion uses an LLM to compact
/// (remove low-value rounds such as failed commands) before emitting the final result.
/// <para>
/// Loop events (text, reasoning, tool calls, permissions, etc.) are forwarded
/// to the caller in real time without modification.
/// </para>
/// </summary>
public abstract class ReadOnlyCompactAgentBase : ISingleClientAgent
{
    public int CallCount { get; set; }

    public int StepRetryCount { get; set; } = 3;

    public MiniSweAgentConfig Config { get; }

    private readonly IReadOnlyList<IAIFunctionGroup> _toolProviders;

    public ILLMChatClient ChatClient { get; }

    public AgentOption AgentOption { get; }

    public abstract string Name { get; }

    protected abstract string TaskCompleteFlag { get; }

    protected abstract string CompactHandoffSeparator { get; }

    protected abstract string CompactPromptTemplate { get; }

    protected abstract string CompactErrorTag { get; }

    protected virtual int CompactTimeoutSeconds => 30;

    protected ReadOnlyCompactAgentBase(ILLMChatClient agent, AgentOption agentOption, MiniSweAgentConfig config)
    {
        ChatClient = agent;
        AgentOption = agentOption;
        Config = config;
        _toolProviders = CreateToolProviders(config);
    }

    // ── Setup result passed from BuildRequestContextAsync to Execute ──

    private readonly record struct SetupResult(
        RequestContext RequestContext,
        string? UserPrompt,
        string? SystemPrompt);

    /// <summary>
    /// Builds the <see cref="RequestContext"/> from the dialog session, filtering to read-only tools only.
    /// Returns <c>null</c> when the session has no actionable request.
    /// </summary>
    private async Task<SetupResult?> BuildRequestContextAsync(
        ITextDialogSession dialogSession,
        CancellationToken cancellationToken)
    {
        var chatHistory = dialogSession.GetHistory();
        if (chatHistory.Count == 0 || chatHistory[^1] is not IRequestItem request)
            return null;

        var contextBuilder = new AgentDialogContextBuilder(chatHistory)
        {
            PlatformId = Config.PlatformId,
            IncludeHistoryMessages = true,
            IncludeToolInstructions = Config.IncludeToolInstructions,
            IncludeRagInstructions = Config.IncludeRagInstructions,
            SystemTemplate = Config.SystemTemplate,
            SystemPrompt = dialogSession.SystemPrompt,
            InstanceTemplate = Config.InstanceTemplate,
        };
        contextBuilder.MapFromRequest(request);
        contextBuilder.FunctionGroups = FilterReadOnlyFunctionGroups(contextBuilder.FunctionGroups);

        string? workingDirectory;
        if (dialogSession is ProjectSessionViewModel projectSession)
        {
            workingDirectory = projectSession.WorkingDirectory;
            contextBuilder.ProjectInformation = projectSession.ParentProject.ProjectInformationPrompt;
            await AddToolProvidersAsync(contextBuilder,
                projectSession.ParentProject.GetInspectorFunctionGroups(),
                cancellationToken);
        }
        else
        {
            workingDirectory = AgentOption.WorkingDirectory;
        }

        contextBuilder.WorkingDirectory = workingDirectory;
        await AddToolProvidersAsync(contextBuilder, _toolProviders, cancellationToken);
        contextBuilder.CallEngine = new MiniSWEFunctionCallEngine(Config);

        var requestContext = await contextBuilder.BuildAsync(ChatClient.Model, cancellationToken);
        return new SetupResult(requestContext, request.UserPrompt, dialogSession.SystemPrompt);
    }

    /*  Pipeline overview:
     *  1. Each round's loop events are forwarded to a publicStep in real time for the UI.
     *  2. Each round's Messages are cached then cleared — the UI never sees intermediate history.
     *  3. On task completion the cached rounds are compacted via LLM to prune low-value rounds.
     *  4. The final publicStep carries the compacted messages for downstream agent consumption.
     */
    public async IAsyncEnumerable<ReactStep> Execute(ITextDialogSession dialogSession,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // ── Phase 1: Build request context ──
        SetupResult? setup;
        Exception? setupError = null;
        try
        {
            setup = await BuildRequestContextAsync(dialogSession, cancellationToken);
        }
        catch (Exception ex)
        {
            setup = null;
            setupError = ex;
        }

        if (setup == null)
        {
            var errorStep = new ReactStep();
            if (setupError != null)
                errorStep.CompleteWithError(setupError);
            else
                errorStep.Complete(new StepResult { IsCompleted = true });
            yield return errorStep;
            yield break;
        }

        var (requestContext, userPrompt, systemPrompt) = setup.Value;

        // ── Phase 2: ReAct loop with message suppression ──
        var cachedRoundMessages = new List<IReadOnlyList<ChatMessage>>();
        var aggregate = new AgentTaskResult();
        var maxContextTokens = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (Config.StepLimit > 0 && CallCount >= Config.StepLimit)
                throw new Exception("Step limit exceeded");

            StepResult? lastResult = null;
            var retryCount = 0;
            while (retryCount < StepRetryCount)
            {
                await foreach (var step in ChatClient.SendRequestAsync(requestContext, cancellationToken))
                {
                    var publicStep = new ReactStep();
                    var forwardTask = ForwardEventsAsync(step, publicStep);
                    yield return publicStep;
                    var result = await forwardTask;

                    if (result == null)
                    {
                        publicStep.Complete(new StepResult { IsCompleted = true });
                        continue;
                    }

                    lastResult = result;

                    // Cache this round's messages before suppressing them on the result.
                    var roundMessages = result.Messages.ToList();
                    cachedRoundMessages.Add(roundMessages);
                    requestContext.ChatMessages.AddRange(roundMessages);
                    aggregate.Add(result);
                    maxContextTokens = Math.Max(maxContextTokens, result.MaxContextTokens);

                    // Fatal: emit all cached messages without compaction
                    if (result.IsCanceled || result.IsInvalidRequest)
                    {
                        publicStep.Complete(CreateFinalStepResult(aggregate, maxContextTokens,
                            cachedRoundMessages.SelectMany(m => m)));
                        yield break;
                    }

                    // Task complete: compact then emit
                    if (IsExitMessage(requestContext.ReadonlyHistory.LastOrDefault()))
                    {
                        var decision = await CompactAsync(userPrompt, systemPrompt,
                            cachedRoundMessages, cancellationToken);
                        publicStep.Complete(CreateFinalStepResult(aggregate, maxContextTokens,
                            BuildCompactedMessages(cachedRoundMessages, decision)));
                        yield break;
                    }

                    // Intermediate round: suppress messages, pass through other metadata
                    result.Messages = [];
                    publicStep.Complete(result);
                }

                if (lastResult?.Exception is AgentFlowException) break;
                if (lastResult?.Exception == null) break;
                retryCount++;
            }

            CallCount++;
        }
    }

    /// <summary>
    /// Forwards all loop events from <paramref name="source"/> to <paramref name="target"/>.
    /// Returns the source step's result after its channel closes; does NOT call Complete on target.
    /// </summary>
    private static async Task<StepResult?> ForwardEventsAsync(ReactStep source, ReactStep target)
    {
        await foreach (var evt in source)
            target.Emit(evt);
        return source.Result;
    }

    private async Task<CompactDecision?> CompactAsync(string? task,
        string? systemPrompt,
        IReadOnlyList<IReadOnlyList<ChatMessage>> roundMessages,
        CancellationToken cancellationToken)
    {
        if (roundMessages.Count == 0) return null;

        var indexedInput = BuildIndexedCompactInput(roundMessages);
        if (string.IsNullOrWhiteSpace(indexedInput)) return null;

        try
        {
            var message = await PromptTemplateRenderer.RenderAsync(CompactPromptTemplate,
                new Dictionary<string, object?>
                {
                    { "task", task },
                    { "contextHint", systemPrompt },
                    { "input", indexedInput }
                });
            var promptAgent = new PromptBasedAgent(ChatClient) { Timeout = TimeSpan.FromSeconds(CompactTimeoutSeconds) };
            var result = await promptAgent.SendRequestAsync(
                DefaultDialogContextBuilder.CreateFromHistory([new RequestViewItem(message)], systemPrompt),
                cancellationToken);

            var jsonResponse = result.FirstTextResponse;
            if (string.IsNullOrWhiteSpace(jsonResponse)) return null;

            var decision = DeserializeCompactDecision(jsonResponse);
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
            Trace.WriteLine($"[{CompactErrorTag} Error]: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Applies the compact decision to produce the final visible message list.
    /// Removes rounds specified by <see cref="CompactDecision.RemoveIndexes"/>, then appends the summary.
    /// Falls back to all rounds if the filtered result would be empty.
    /// </summary>
    private IReadOnlyList<ChatMessage> BuildCompactedMessages(
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
                : $"\n\n{CompactHandoffSeparator}\n{decision.Summary}";
            messages.Add(new ChatMessage(ChatRole.Assistant, summaryText));
        }

        return messages;
    }

    private static string BuildIndexedCompactInput(IReadOnlyList<IReadOnlyList<ChatMessage>> roundMessages)
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

    private static CompactDecision? DeserializeCompactDecision(string jsonResponse)
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

    private static StepResult CreateFinalStepResult(AgentTaskResult aggregate, int maxContextTokens,
        IEnumerable<ChatMessage> messages)
    {
        return new StepResult
        {
            Usage = aggregate.Usage,
            LastSuccessfulUsage = aggregate.LastSuccessfulUsage,
            FinishReason = aggregate.FinishReason,
            Duration = aggregate.Duration,
            Messages = messages.ToList(),
            ProtocolLog = aggregate.ProtocolLog,
            Latency = aggregate.Latency,
            Price = aggregate.Price,
            Exception = aggregate.Exception,
            Annotations = aggregate.Annotations,
            AdditionalProperties = aggregate.AdditionalProperties,
            IsCompleted = true,
            MaxContextTokens = maxContextTokens,
        };
    }

    private string EnsureCompletionFlag(string summary)
    {
        return summary.Contains(TaskCompleteFlag, StringComparison.Ordinal)
            ? summary
            : $"{summary.TrimEnd()}\n\n{TaskCompleteFlag}";
    }

    protected static MiniSweAgentConfig CreateBaseConfig(ILLMChatClient agent, AgentOption agentOption)
    {
        return agentOption.Platform switch
        {
            AgentPlatform.Windows => MiniSweAgentConfigLoader.LoadDefaultWindowsConfig(),
            AgentPlatform.Linux => agent.Model.SupportFunctionCall
                ? MiniSweAgentConfigLoader.LoadDefaultLinuxToolCallConfig()
                : MiniSweAgentConfigLoader.LoadDefaultLinuxTextBasedConfig(),
            AgentPlatform.Wsl => MiniSweAgentConfigLoader.LoadDefaultWslConfig(),
            _ => throw new ArgumentOutOfRangeException(nameof(agentOption.Platform)),
        };
    }

    protected static string BuildProjectContextPlaceholders()
    {
        return """
               {{{project_information}}}

               {{{platform_instructions}}}

               {{{tool_instructions}}}

               {{{rag_instructions}}}
               """;
    }

    protected static string BuildReadOnlyToolPriorities()
    {
        return """
               Tool priorities:
               - Prefer ProjectAwareness for solution/project/file overview.
               - Prefer SymbolSemantic for symbol relationships and impact analysis.
               - Prefer CodeReading for focused file and symbol inspection.
               - Prefer CodeSearch when symbol names are unknown.
               - Prefer CLI only for VCS or environment inspection.
               """;
    }

    private static IReadOnlyList<IAIFunctionGroup> CreateToolProviders(MiniSweAgentConfig config)
    {
        var providers = new List<IAIFunctionGroup>();
        switch (config.PlatformId)
        {
            case AgentPlatform.Wsl:
            case AgentPlatform.Linux:
                providers.Add(new WslCLIPlugin
                {
                    WslDistributionName = config.WslDistributionName,
                    WslUserName = config.WslUserName,
                    TimeoutSeconds = config.ExecutionTimeout,
                    MapWorkingDirectoryToWsl = config.MapWorkingDirectoryToWsl,
                });
                break;
            case AgentPlatform.Windows:
            default:
                providers.Add(new WinCLIPlugin());
                break;
        }

        return providers;
    }

    private static async Task AddToolProvidersAsync(AgentDialogContextBuilder contextBuilder,
        IEnumerable<IAIFunctionGroup> toolProviders,
        CancellationToken cancellationToken)
    {
        var safeProviders = toolProviders
            .Where(IsReadOnlyGroup)
            .ToArray();
        if (safeProviders.Length == 0) return;

        contextBuilder.FunctionGroups ??= [];
        var functionGroups = contextBuilder.FunctionGroups;
        foreach (var toolProvider in safeProviders)
        {
            if (!functionGroups.Contains(toolProvider, AIFunctionGroupComparer.Instance))
            {
                var tree = await toolProvider.ToCheckableFunctionGroupTree(cancellationToken);
                functionGroups.Add(tree);
            }
        }
    }

    private static List<CheckableFunctionGroupTree>? FilterReadOnlyFunctionGroups(
        List<CheckableFunctionGroupTree>? functionGroups)
    {
        if (functionGroups == null || functionGroups.Count == 0) return functionGroups;

        return functionGroups
            .Where(IsReadOnlyGroup)
            .Select(group => (CheckableFunctionGroupTree)group.Clone())
            .ToList();
    }

    private static bool IsReadOnlyGroup(IAIFunctionGroup functionGroup)
    {
        var rawGroup = functionGroup is CheckableFunctionGroupTree tree ? tree.Data : functionGroup;
        return rawGroup is ProjectAwarenessPlugin
            or SymbolSemanticPlugin
            or CodeSearchPlugin
            or CodeReadingPlugin
            or WinCLIPlugin
            or WslCLIPlugin
            or GoogleSearchPlugin
            or UrlFetcherPlugin;
    }

    private bool IsExitMessage(ChatMessage? message)
    {
        if (string.IsNullOrEmpty(message?.Text)) return false;
        return message.Text.Contains(Config.TaskCompleteFlag, StringComparison.Ordinal);
    }

    private sealed class CompactDecision
    {
        [JsonPropertyName("removeIndexes")]
        public List<int>? RemoveIndexes { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }
    }
}
