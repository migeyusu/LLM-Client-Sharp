using System.ComponentModel;
using LLMClient.Abstraction;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.Project;
using LLMClient.ToolCall;
using LLMClient.ToolCall.DefaultPlugins;

namespace LLMClient.Agent.MiniSWE;

/// <summary>
/// Mini-SWE-Agent — full-access ReAct loop with filesystem and CLI tools.
/// </summary>
[Description("MiniSWE Agent")]
public class MiniSweAgent : ReactAgentBase, IInbuiltAgent
{
    private readonly IReadOnlyList<KernelFunctionGroup> _toolProviders;

    public override string Name { get; } = "MiniSWE Agent";

    public MiniSweAgent(ILLMChatClient agent, AgentOption agentOption)
        : base(agent, agentOption, CreateConfig(agent, agentOption))
    {
        _toolProviders = CreateToolProviders(Config);
    }

    protected override async Task<RequestContext?> BuildRequestContextAsync(ISession dialogSession,
        CancellationToken cancellationToken)
    {
        var contextBuilder = AgentRequestContextBuilder.CreateFromSession(dialogSession, Config);
        string? workingDirectory;

        if (dialogSession is IProjectSession projectSession)
        {
            workingDirectory = projectSession.WorkingDirectory;
            contextBuilder.ProjectInformation = projectSession.ProjectInformationPrompt;
        }
        else
        {
            workingDirectory = AgentOption.WorkingDirectory;
        }

        contextBuilder.WorkingDirectory = workingDirectory;

        if (_toolProviders.Count > 0)
        {
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                _toolProviders.OfType<FileSystemPlugin>()
                    .FirstOrDefault()?.BypassPaths.Add(workingDirectory);
            }

            contextBuilder.FunctionGroups ??= [];
            var functionGroups = contextBuilder.FunctionGroups;
            foreach (var toolProvider in _toolProviders)
            {
                if (!functionGroups.Contains(toolProvider, AIFunctionGroupComparer.Instance))
                {
                    var tree = await toolProvider.ToCheckableFunctionGroupTree(cancellationToken);
                    functionGroups.Add(tree);
                }
            }
        }

        contextBuilder.CallEngine = new MiniSWEFunctionCallEngine(Config);
        return await contextBuilder.BuildAsync(ChatClient.Model, cancellationToken);
    }

    private static MiniSweAgentConfig CreateConfig(ILLMChatClient agent, AgentOption agentOption) =>
        agentOption.Platform switch
        {
            RunPlatform.Windows => MiniSweAgentConfigLoader.LoadDefaultWindowsConfig(),
            RunPlatform.Linux => agent.Model.SupportFunctionCall
                ? MiniSweAgentConfigLoader.LoadDefaultLinuxToolCallConfig()
                : MiniSweAgentConfigLoader.LoadDefaultLinuxTextBasedConfig(),
            RunPlatform.Wsl => MiniSweAgentConfigLoader.LoadDefaultWslConfig(),
            _ => throw new ArgumentOutOfRangeException(nameof(agentOption.Platform)),
        };

    private static IReadOnlyList<KernelFunctionGroup> CreateToolProviders(MiniSweAgentConfig config)
    {
        var providers = new List<KernelFunctionGroup>();
        switch (config.PlatformId)
        {
            case RunPlatform.Wsl:
            case RunPlatform.Linux:
                providers.Add(new WslCLIPlugin
                {
                    WslDistributionName = config.WslDistributionName,
                    WslUserName = config.WslUserName,
                    TimeoutSeconds = config.ExecutionTimeout,
                    MapWorkingDirectoryToWsl = config.MapWorkingDirectoryToWsl,
                });
                break;
            case RunPlatform.Windows:
            default:
                providers.Add(new WinCLIPlugin());
                break;
        }

        providers.Add(new FileSystemPlugin());
        return providers;
    }
}