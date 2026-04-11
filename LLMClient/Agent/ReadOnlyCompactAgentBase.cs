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
/// Shared read-only agent pipeline that captures all internal loops, compacts history, and emits a single final step.
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

    /*  功能说明:
     *  1. 每轮 step 的 loop 事件实时转发给 publicStep，供 UI 实时观察
     *  2. 每轮结果的 Messages 缓存后清空再 Complete publicStep，不向 UI 暴露中间历史
     *  3. 所有轮次的 ChatMessage 按轮缓存，任务结束时 compact 剔除冗余轮次
     *  4. 最终生成包含 compact 后历史和摘要的 StepResult，通过最后一个 publicStep 输出
     */
    public async IAsyncEnumerable<ReactStep> Execute(ITextDialogSession dialogSession,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chatHistory = dialogSession.GetHistory();
        if (chatHistory.Count == 0 || chatHistory[^1] is not IRequestItem request)
        {
            var emptyStep = new ReactStep();
            emptyStep.Complete(new StepResult { IsCompleted = true });
            yield return emptyStep;
            yield break;
        }

        RequestContext? requestContext = null;
        Exception? setupError = null;
        try
        {
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
            requestContext = await contextBuilder.BuildAsync(ChatClient.Model, cancellationToken);
        }
        catch (Exception ex)
        {
            setupError = ex;
        }

        if (setupError != null || requestContext == null)
        {
            var faultedStep = new ReactStep();
            faultedStep.CompleteWithError(setupError ?? new InvalidOperationException("Failed to build request context"));
            yield return faultedStep;
            yield break;
        }

        // cachedRoundMessages[i] holds the ChatMessages from round i, used for compaction.
        // aggregate accumulates metadata (usage, price, etc.) across all rounds.
        var cachedRoundMessages = new List<IReadOnlyList<ChatMessage>>();
        var aggregate = new AgentTaskResult();
        var maxContextTokens = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (Config.StepLimit > 0 && CallCount >= Config.StepLimit)
            {
                throw new Exception("Step limit exceeded");
            }

            StepResult? lastResult = null;
            var retryCount = 0;
            while (retryCount < StepRetryCount)
            {
                await foreach (var step in ChatClient.SendRequestAsync(requestContext, cancellationToken))
                {
                    var publicStep = new ReactStep();
                    // Start forwarding events immediately; yield publicStep so the UI observes in real time.
                    // publicStep.Complete() is called below, after the forwarding task finishes.
                    var forwardTask = ForwardLoopEventsAsync(step, publicStep);
                    yield return publicStep;
                    var result = await forwardTask;

                    if (result == null)
                    {
                        publicStep.Complete(new StepResult { IsCompleted = true });
                        continue;
                    }

                    lastResult = result;

                    // Cache this round's messages before clearing them on the result object.
                    var roundMessages = result.Messages.ToList();
                    cachedRoundMessages.Add(roundMessages);
                    requestContext.ChatMessages.AddRange(roundMessages); // MiniSwe-style history accumulation
                    aggregate.Add(result);
                    maxContextTokens = Math.Max(maxContextTokens, result.MaxContextTokens);

                    if (result.IsCanceled || result.IsInvalidRequest)
                    {
                        var allMessages = cachedRoundMessages.SelectMany(m => m).ToList();
                        publicStep.Complete(CreateFinalStepResult(aggregate, maxContextTokens, allMessages));
                        yield break;
                    }

                    if (IsExitMessage(requestContext.ReadonlyHistory.LastOrDefault()))
                    {
                        var decision = await CompactAsync(request.UserPrompt,
                            dialogSession.SystemPrompt,
                            cachedRoundMessages,
                            cancellationToken);
                        publicStep.Complete(CreateFinalStepResult(aggregate, maxContextTokens,
                            BuildCompactedMessages(cachedRoundMessages, decision)));
                        yield break;
                    }

                    // Intermediate round: suppress messages so the UI only receives the final compacted history.
                    result.Messages = [];
                    publicStep.Complete(result);
                }

                if (lastResult?.Exception is AgentFlowException) break;
                if (lastResult?.Exception == null) break;
                retryCount++;
            }

            CallCount++;

            if (IsExitMessage(requestContext.ReadonlyHistory.LastOrDefault()))
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// Forwards all loop events from <paramref name="step"/> to <paramref name="publicStep"/>.
    /// Permission requests are handled inline and are not forwarded.
    /// Returns when the internal step's channel closes; does NOT call Complete on publicStep.
    /// </summary>
    private static async Task<StepResult?> ForwardLoopEventsAsync(ReactStep step, ReactStep publicStep)
    {
        await foreach (var loopEvent in step)
        {
            if (loopEvent is PermissionRequest permissionRequest)
            {
                var allowed = await InvokePermissionDialog.RequestAsync(permissionRequest.Content);
                permissionRequest.Response.SetResult(allowed);
            }
            else
            {
                publicStep.Emit(loopEvent);
            }
        }

        return step.Result;
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
