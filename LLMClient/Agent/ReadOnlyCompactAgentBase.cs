using LLMClient.Abstraction;
using LLMClient.Agent.MiniSWE;
using LLMClient.ContextEngineering.Tools;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.Project;
using LLMClient.ToolCall;
using LLMClient.ToolCall.DefaultPlugins;

namespace LLMClient.Agent;

/// <summary>
/// Read-only agent base that runs a ReAct loop restricted to inspection-safe tool groups.
/// Subclasses configure the prompt templates via <see cref="MiniSweAgentConfig"/>.
/// </summary>
public abstract class ReadOnlyCompactAgentBase : ReactAgentBase
{
    private readonly IReadOnlyList<IAIFunctionGroup> _toolProviders;

    protected ReadOnlyCompactAgentBase(ILLMChatClient agent, AgentOption agentOption, MiniSweAgentConfig config)
        : base(agent, agentOption, config)
    {
        _toolProviders = CreateToolProviders(config);
    }

    protected override async Task<RequestContext?> BuildRequestContextAsync(
        ITextDialogSession dialogSession,
        CancellationToken cancellationToken)
    {
        var contextBuilder = AgentRequestContextBuilder.CreateFromSession(dialogSession, Config);
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

        return await contextBuilder.BuildAsync(ChatClient.Model, cancellationToken);
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

    protected static string BuildProjectContextPlaceholders() =>
        """
        {{{project_information}}}

        {{{platform_instructions}}}

        {{{tool_instructions}}}

        {{{rag_instructions}}}
        """;

    protected static string BuildReadOnlyToolPriorities() =>
        """
        Tool priorities:
        - Prefer ProjectAwareness for solution/project/file overview.
        - Prefer SymbolSemantic for symbol relationships and impact analysis.
        - Prefer CodeReading for focused file and symbol inspection.
        - Prefer CodeSearch when symbol names are unknown.
        - Prefer CLI only for VCS or environment inspection.
        """;

    private static IReadOnlyList<IAIFunctionGroup> CreateToolProviders(MiniSweAgentConfig config)
    {
        return config.PlatformId switch
        {
            AgentPlatform.Wsl or AgentPlatform.Linux =>
            [
                new WslCLIPlugin
                {
                    WslDistributionName = config.WslDistributionName,
                    WslUserName = config.WslUserName,
                    TimeoutSeconds = config.ExecutionTimeout,
                    MapWorkingDirectoryToWsl = config.MapWorkingDirectoryToWsl,
                }
            ],
            _ => [new WinCLIPlugin()],
        };
    }

    private static async Task AddToolProvidersAsync(
        AgentRequestContextBuilder contextBuilder,
        IEnumerable<IAIFunctionGroup> toolProviders,
        CancellationToken cancellationToken)
    {
        var safeProviders = toolProviders.Where(IsReadOnlyGroup).ToArray();
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
        if (functionGroups is not { Count: > 0 }) return functionGroups;
        return functionGroups
            .Where(IsReadOnlyGroup)
            .Select(g => (CheckableFunctionGroupTree)g.Clone())
            .ToList();
    }

    private static bool IsReadOnlyGroup(IAIFunctionGroup functionGroup)
    {
        var raw = functionGroup is CheckableFunctionGroupTree tree ? tree.Data : functionGroup;
        return raw is ProjectAwarenessPlugin
            or SymbolSemanticPlugin
            or CodeSearchPlugin
            or CodeReadingPlugin
            or WinCLIPlugin
            or WslCLIPlugin
            or GoogleSearchPlugin
            or UrlFetcherPlugin;
    }
}