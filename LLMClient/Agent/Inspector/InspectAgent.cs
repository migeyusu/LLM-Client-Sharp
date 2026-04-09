using System.ComponentModel;
using System.Runtime.CompilerServices;
using LLMClient.Abstraction;
using LLMClient.Agent.MiniSWE;
using LLMClient.ContextEngineering.Tools;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.Project;
using LLMClient.ToolCall;
using LLMClient.ToolCall.DefaultPlugins;
using Microsoft.Extensions.AI;

namespace LLMClient.Agent.Inspector;

/// <summary>
/// Inspector agent that gathers structured project and code context for downstream agents.
/// It is intentionally read-only and only exposes inspection-safe tool groups.
/// </summary>
[Description("Inspect Agent")]
public class InspectAgent : ISingleClientAgent
{
    private const string InspectionCompleteFlag = "INSPECTION_COMPLETE";

    public int CallCount { get; set; }

    public int StepRetryCount { get; set; } = 3;

    public MiniSweAgentConfig Config { get; }

    private readonly IReadOnlyList<IAIFunctionGroup> _toolProviders;

    public ILLMChatClient ChatClient { get; }

    public AgentOption AgentOption { get; }

    public InspectAgent(ILLMChatClient agent, AgentOption agentOption)
    {
        ChatClient = agent;
        AgentOption = agentOption;
        Config = CreateConfig(agent, agentOption);
        _toolProviders = CreateToolProviders(Config);
    }

    public string Name { get; } = "Inspect Agent";

    public async IAsyncEnumerable<ReactStep> Execute(ITextDialogSession dialogSession,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chatHistory = dialogSession.GetHistory();
        if (chatHistory.Count == 0 || chatHistory[^1] is not IRequestItem request)
        {
            yield break;
        }

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
        contextBuilder.FunctionGroups = FilterInspectFunctionGroups(contextBuilder.FunctionGroups);

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
        while (!cancellationToken.IsCancellationRequested)
        {
            if (Config.StepLimit > 0 && CallCount >= Config.StepLimit)
            {
                throw new Exception("Step limit exceeded");
            }

            StepResult? lastStepResult = null;
            var retryCount = 0;
            while (retryCount < StepRetryCount)
            {
                await foreach (var step in ChatClient.SendRequestAsync(requestContext, cancellationToken))
                {
                    yield return step;
                    if (step.Result != null)
                    {
                        requestContext.ChatMessages.AddRange(step.Result.Messages);
                        lastStepResult = step.Result;
                    }
                }

                if (lastStepResult?.IsCanceled == true || lastStepResult?.IsInvalidRequest == true)
                {
                    yield break;
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

            var lastMessage = requestContext.ReadonlyHistory.LastOrDefault();
            if (IsExitMessage(lastMessage))
            {
                break;
            }
        }
    }

    private static MiniSweAgentConfig CreateConfig(ILLMChatClient agent, AgentOption agentOption)
    {
        MiniSweAgentConfig config = agentOption.Platform switch
        {
            AgentPlatform.Windows => MiniSweAgentConfigLoader.LoadDefaultWindowsConfig(),
            AgentPlatform.Linux => agent.Model.SupportFunctionCall
                ? MiniSweAgentConfigLoader.LoadDefaultLinuxToolCallConfig()
                : MiniSweAgentConfigLoader.LoadDefaultLinuxTextBasedConfig(),
            AgentPlatform.Wsl => MiniSweAgentConfigLoader.LoadDefaultWslConfig(),
            _ => throw new ArgumentOutOfRangeException(nameof(agentOption.Platform)),
        };

        config.TaskCompleteFlag = InspectionCompleteFlag;
        config.IncludeToolInstructions = true;
        config.IncludeRagInstructions = true;
        config.StepLimit = 8;
        config.SystemTemplate = """
            You are an Inspector agent in a multi-agent software workflow.

            Your responsibility is to understand the task, collect the minimum necessary context,
            and hand off a reliable investigation summary for downstream agents.

            Core rules:
            - You are read-only. Do not modify files, create files, or apply patches.
            - Prefer structured code intelligence tools over ad-hoc shell inspection.
            - Start broad, then narrow down based on evidence.
            - Use project-awareness, code-reading, symbol-analysis, and code-search tools to understand the workspace.
            - Use CLI only for safe inspection tasks such as git status, directory inspection, or non-destructive project metadata commands.
            - Do not guess. Trace symbols and files before concluding.
            - When enough context has been gathered, produce a concise final inspection report and include the flag INSPECTION_COMPLETE.

            Expected output focus:
            - relevant projects / modules
            - likely files and symbols
            - important dependencies and call paths
            - uncertainties or missing context

            {{{project_information}}}

            {{{platform_instructions}}}

            {{{tool_instructions}}}

            {{{rag_instructions}}}
            """;
        config.InstanceTemplate = """
            Please inspect the following task and gather the context needed by later agents.

            <task>
            {{task}}
            </task>

            Inspection workflow:
            1. Identify the relevant project or subsystem.
            2. Explore structure and conventions.
            3. Search for candidate files, types, and symbols.
            4. Read only the most relevant code or symbol bodies.
            5. Summarize findings, risks, and next implementation targets.

            Tool priorities:
            - Prefer ProjectAwareness for solution/project/file overview.
            - Prefer SymbolSemantic for symbol relationships and impact analysis.
            - Prefer CodeReading for focused file and symbol inspection.
            - Prefer CodeSearch when symbol names are unknown.
            - Prefer CLI only for VCS or environment inspection.

            Finish once you can provide an actionable inspection summary for another agent.
            Your final response must include INSPECTION_COMPLETE.
            """;
        return config;
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

    private static List<CheckableFunctionGroupTree>? FilterInspectFunctionGroups(
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
            return true;
        }

        return message.Text.Contains(Config.TaskCompleteFlag, StringComparison.Ordinal);
    }
}
