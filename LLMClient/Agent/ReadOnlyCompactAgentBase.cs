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
/*  要求实现的功能：
 *  1. ChatClient.SendRequestAsync返回的step可以被外界实时观察到
 *  2. 拦截step.result里的Messages，使之为空，并缓存原始数据
 *  3. 最终compact原始历史记录，将历史记录进行有效剔除
 *  4. 生成一个新的StepResult，包含compact后的历史记录和最终的summary，供外界观察到
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

        var compactCandidates = new List<CompactCandidate>();
        var bufferedSteps = new List<BufferedStep>();
        StepResult? lastStepResult = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (Config.StepLimit > 0 && CallCount >= Config.StepLimit)
            {
                throw new Exception("Step limit exceeded");
            }

            lastStepResult = null;
            var retryCount = 0;
            while (retryCount < StepRetryCount)
            {
                await foreach (var internalStep in ChatClient.SendRequestAsync(requestContext, cancellationToken))
                {
                    var publicStep = new ReactStep();
                    // Start processing immediately so loop events are forwarded in real time.
                    var processingTask = ProcessPublicStepAsync(internalStep,
                        publicStep,
                        request,
                        dialogSession.SystemPrompt,
                        requestContext,
                        compactCandidates,
                        bufferedSteps,
                        cancellationToken);

                    yield return publicStep;

                    var stepOutcome = await processingTask;
                    lastStepResult = stepOutcome.Result;
                    if (stepOutcome.IsTerminal)
                    {
                        yield break;
                    }
                }

                if (lastStepResult?.Exception is AgentFlowException)
                {
                    break;
                }

                if (lastStepResult?.Exception == null)
                {
                    break;
                }

                retryCount++;
            }

            CallCount++;

            if (IsExitMessage(requestContext.ReadonlyHistory.LastOrDefault()))
            {
                yield break;
            }
        }
    }

    private static CompactCandidate CreateCompactCandidateFromResult(StepResult result, int compactIndex)
    {
        var content = result.Messages
            .Select(message => message.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Aggregate(new StringBuilder(), (builder, text) => builder.AppendLine(text), builder =>
            {
                var output = builder.ToString().Trim();
                return string.IsNullOrWhiteSpace(output)
                    ? "[NoMessageContent]"
                    : output;
            });

        return new CompactCandidate
        {
            Index = compactIndex,
            LoopNumber = compactIndex + 1,
            Content = content,
        };
    }

    private async Task<StepOutcome> ProcessPublicStepAsync(ReactStep internalStep,
        ReactStep publicStep,
        IRequestItem request,
        string? systemPrompt,
        RequestContext requestContext,
        List<CompactCandidate> compactCandidates,
        List<BufferedStep> bufferedSteps,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var loopEvent in internalStep.WithCancellation(cancellationToken))
            {
                if (loopEvent is PermissionRequest permissionRequest)
                {
                    var allowed = await InvokePermissionDialog.RequestAsync(permissionRequest.Content);
                    permissionRequest.Response.SetResult(allowed);
                    continue;
                }

                publicStep.Emit(loopEvent);
            }

            if (internalStep.Result == null)
            {
                publicStep.Complete(new StepResult { IsCompleted = true });
                return new StepOutcome(null, false);
            }

            var rawResult = internalStep.Result;
            var sanitizedResult = SanitizeStepResult(rawResult);
            var candidate = CreateCompactCandidateFromResult(rawResult, compactCandidates.Count);
            compactCandidates.Add(candidate);
            bufferedSteps.Add(new BufferedStep
            {
                Candidate = candidate,
                Result = rawResult,
            });

            // Internal history uses sanitized messages to avoid failed tool-call pollution.
            requestContext.ChatMessages.AddRange(sanitizedResult.Messages);

            if (sanitizedResult.IsCanceled || sanitizedResult.IsInvalidRequest)
            {
                var fallback = CreateFinalStepResult(bufferedSteps, BuildFallbackMessages(bufferedSteps))
                               ?? rawResult;
                publicStep.Complete(fallback);
                return new StepOutcome(sanitizedResult, true);
            }

            if (IsExitMessage(requestContext.ReadonlyHistory.LastOrDefault()))
            {
                var compactDecision = await CompactAsync(request.UserPrompt,
                    systemPrompt,
                    compactCandidates,
                    cancellationToken);
                var visibleMessages = BuildVisibleMessages(bufferedSteps, compactDecision);
                var finalResult = CreateFinalStepResult(bufferedSteps, visibleMessages)
                                  ?? rawResult;
                publicStep.Complete(finalResult);
                return new StepOutcome(sanitizedResult, true);
            }

            // Intercept visible messages for non-terminal loops, while preserving loop metadata.
            publicStep.Complete(new StepResult
            {
                Usage = rawResult.Usage,
                LastSuccessfulUsage = rawResult.LastSuccessfulUsage,
                FinishReason = rawResult.FinishReason,
                Duration = rawResult.Duration,
                Messages = [],
                ProtocolLog = rawResult.ProtocolLog,
                Latency = rawResult.Latency,
                Price = rawResult.Price,
                Exception = rawResult.Exception,
                Annotations = rawResult.Annotations,
                AdditionalProperties = rawResult.AdditionalProperties,
                IsCompleted = rawResult.IsCompleted,
                MaxContextTokens = rawResult.MaxContextTokens,
            });

            return new StepOutcome(sanitizedResult, false);
        }
        catch (OperationCanceledException)
        {
            var cancel = new StepResult
            {
                Exception = new OperationCanceledException(),
                IsCompleted = true,
            };
            publicStep.Complete(cancel);
            return new StepOutcome(cancel, true);
        }
        catch (Exception ex)
        {
            var error = new StepResult
            {
                Exception = ex,
                IsCompleted = true,
            };
            publicStep.Complete(error);
            return new StepOutcome(error, true);
        }
    }

    private async Task<CompactDecision?> CompactAsync(string? task,
        string? systemPrompt,
        IReadOnlyList<CompactCandidate> compactCandidates,
        CancellationToken cancellationToken)
    {
        if (compactCandidates.Count == 0)
        {
            return null;
        }

        var indexedInput = BuildIndexedCompactInput(compactCandidates);
        if (string.IsNullOrWhiteSpace(indexedInput))
        {
            return null;
        }

        try
        {
            var message = await PromptTemplateRenderer.RenderAsync(CompactPromptTemplate,
                new Dictionary<string, object?>
                {
                    { "task", task },
                    { "contextHint", systemPrompt },
                    { "input", indexedInput }
                });
            var promptAgent = new PromptBasedAgent(ChatClient)
            {
                Timeout = TimeSpan.FromSeconds(CompactTimeoutSeconds),
            };
            var result = await promptAgent.SendRequestAsync(DefaultDialogContextBuilder.CreateFromHistory([
                    new RequestViewItem(message)
                ], systemPrompt),
                cancellationToken);
            var jsonResponse = result.FirstTextResponse;
            if (string.IsNullOrWhiteSpace(jsonResponse))
            {
                return null;
            }

            var decision = DeserializeCompactDecision(jsonResponse);
            if (decision == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(decision.Summary))
            {
                decision.Summary = EnsureCompletionFlag(decision.Summary.Trim());
            }

            if (decision.RemoveIndexes != null)
            {
                decision.RemoveIndexes = decision.RemoveIndexes
                    .Distinct()
                    .Where(index => index >= 0 && index < compactCandidates.Count)
                    .OrderBy(index => index)
                    .ToList();
            }

            return decision;
        }
        catch (OperationCanceledException)
        {
            throw; // Let cancellation propagate — do not swallow.
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[{CompactErrorTag} Error]: {ex.Message}");
            return null;
        }
    }

    private static CompactDecision? DeserializeCompactDecision(string jsonResponse)
    {
        var json = ExtractJsonObject(jsonResponse);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<CompactDecision>(json);
    }

    private static string? ExtractJsonObject(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return response[start..(end + 1)];
    }

    private static string BuildIndexedCompactInput(IReadOnlyList<CompactCandidate> compactCandidates)
    {
        var builder = new StringBuilder();
        foreach (var candidate in compactCandidates)
        {
            builder.AppendLine($"[{candidate.Index}]|loop={candidate.LoopNumber}");
            builder.AppendLine(candidate.Content);
            builder.AppendLine("---");
        }

        return builder.ToString().TrimEnd();
    }

    private static IReadOnlyList<ChatMessage> BuildFallbackMessages(IReadOnlyList<BufferedStep> bufferedSteps)
    {
        return bufferedSteps
            .Where(step => step.Result != null)
            .SelectMany(step => step.Result!.Messages)
            .ToList();
    }

    private IReadOnlyList<ChatMessage> BuildVisibleMessages(IReadOnlyList<BufferedStep> bufferedSteps,
        CompactDecision? compactDecision)
    {
        var historyMessages = BuildHistoryMessages(bufferedSteps, compactDecision?.RemoveIndexes);
        if (string.IsNullOrWhiteSpace(compactDecision?.Summary))
        {
            return historyMessages;
        }

        var visibleMessages = historyMessages.ToList();
        var summaryText = visibleMessages.Count == 0
            ? compactDecision.Summary
            : $"\n\n{CompactHandoffSeparator}\n{compactDecision.Summary}";
        visibleMessages.Add(new ChatMessage(ChatRole.Assistant, summaryText));
        return visibleMessages;
    }

    private static IReadOnlyList<ChatMessage> BuildHistoryMessages(IReadOnlyList<BufferedStep> bufferedSteps,
        IReadOnlyList<int>? removeIndexes)
    {
        if (removeIndexes == null || removeIndexes.Count == 0)
        {
            return BuildFallbackMessages(bufferedSteps);
        }

        var removeIndexSet = removeIndexes.ToHashSet();
        var filteredMessages = bufferedSteps
            .Where(step => !removeIndexSet.Contains(step.Candidate.Index) && step.Result != null)
            .SelectMany(step => step.Result!.Messages)
            .ToList();
        return filteredMessages.Count > 0
            ? filteredMessages
            : BuildFallbackMessages(bufferedSteps);
    }

    private static StepResult? CreateFinalStepResult(IReadOnlyList<BufferedStep> bufferedSteps,
        IReadOnlyList<ChatMessage> visibleMessages)
    {
        var completedResults = bufferedSteps
            .Where(step => step.Result != null)
            .Select(step => step.Result!)
            .ToList();
        if (completedResults.Count == 0)
        {
            return null;
        }

        var aggregate = new AgentTaskResult();
        foreach (var result in completedResults)
        {
            aggregate.Add(result);
        }

        return new StepResult
        {
            Usage = aggregate.Usage,
            LastSuccessfulUsage = aggregate.LastSuccessfulUsage,
            FinishReason = aggregate.FinishReason,
            Duration = aggregate.Duration,
            Messages = visibleMessages,
            ProtocolLog = aggregate.ProtocolLog,
            Latency = aggregate.Latency,
            Price = aggregate.Price,
            Exception = aggregate.Exception,
            Annotations = aggregate.Annotations,
            AdditionalProperties = aggregate.AdditionalProperties,
            IsCompleted = completedResults.All(result => result.IsCompleted),
            MaxContextTokens = completedResults.Max(result => result.MaxContextTokens),
        };
    }

    private static StepResult SanitizeStepResult(StepResult result)
    {
        var failedCallIds = result.Messages
            .SelectMany(message => message.Contents.OfType<FunctionResultContent>())
            .Where(content => content.Exception != null)
            .Select(content => content.CallId)
            .Where(callId => !string.IsNullOrWhiteSpace(callId))
            .ToHashSet(StringComparer.Ordinal);

        if (failedCallIds.Count == 0)
        {
            return result;
        }

        var messages = result.Messages
            .Where(message => !ContainsFailedToolContent(message, failedCallIds))
            .ToList();

        return new StepResult
        {
            Usage = result.Usage,
            LastSuccessfulUsage = result.LastSuccessfulUsage,
            FinishReason = result.FinishReason,
            Duration = result.Duration,
            Messages = messages,
            ProtocolLog = result.ProtocolLog,
            Latency = result.Latency,
            Price = result.Price,
            Exception = result.Exception,
            Annotations = result.Annotations,
            AdditionalProperties = result.AdditionalProperties,
            IsCompleted = result.IsCompleted,
            MaxContextTokens = result.MaxContextTokens,
        };
    }

    private static bool ContainsFailedToolContent(ChatMessage message, IReadOnlySet<string> failedCallIds)
    {
        foreach (var call in message.Contents.OfType<FunctionCallContent>())
        {
            if (!string.IsNullOrWhiteSpace(call.CallId) && failedCallIds.Contains(call.CallId))
            {
                return true;
            }
        }

        foreach (var result in message.Contents.OfType<FunctionResultContent>())
        {
            if (!string.IsNullOrWhiteSpace(result.CallId) && failedCallIds.Contains(result.CallId))
            {
                return true;
            }
        }

        return false;
    }

    private string EnsureCompletionFlag(string summary)
    {
        if (summary.Contains(TaskCompleteFlag, StringComparison.Ordinal))
        {
            return summary;
        }

        return $"{summary.TrimEnd()}\n\n{TaskCompleteFlag}";
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
        if (safeProviders.Length == 0)
        {
            return;
        }

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
        if (functionGroups == null || functionGroups.Count == 0)
        {
            return functionGroups;
        }

        return functionGroups
            .Where(IsReadOnlyGroup)
            .Select(group => (CheckableFunctionGroupTree)group.Clone())
            .ToList();
    }

    private static bool IsReadOnlyGroup(IAIFunctionGroup functionGroup)
    {
        var rawGroup = functionGroup is CheckableFunctionGroupTree tree
            ? tree.Data
            : functionGroup;

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
        if (string.IsNullOrEmpty(message?.Text))
        {
            return false;
        }

        return message.Text.Contains(Config.TaskCompleteFlag, StringComparison.Ordinal);
    }

    private sealed class BufferedStep
    {
        public required CompactCandidate Candidate { get; init; }

        public StepResult? Result { get; init; }
    }

    private sealed class CompactCandidate
    {
        public required int Index { get; init; }

        public required int LoopNumber { get; init; }

        public required string Content { get; init; }
    }

    private sealed class CompactDecision
    {
        [JsonPropertyName("removeIndexes")]
        public List<int>? RemoveIndexes { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }
    }

    private sealed class StepOutcome
    {
        public StepOutcome(StepResult? result, bool isTerminal)
        {
            Result = result;
            IsTerminal = isTerminal;
        }

        public StepResult? Result { get; }

        public bool IsTerminal { get; }
    }

}

