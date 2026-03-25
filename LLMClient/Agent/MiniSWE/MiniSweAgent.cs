using System.ComponentModel;
using System.Runtime.CompilerServices;
using LLMClient.Abstraction;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.Project;
using LLMClient.ToolCall.DefaultPlugins;
using Microsoft.Extensions.AI;

namespace LLMClient.Agent.MiniSWE;

/// <summary>
/// Mini-SWE-Agent 核心ReAct循环实现
/// </summary>
[Description("MiniSWE Agent")]
public class MiniSweAgent : IAgent
{
    public int CallCount { get; set; }

    /// <summary>
    /// 每个步骤重试次数
    /// </summary>
    public int StepRetryCount { get; set; } = 3;

    public MiniSweAgentConfig Config { get; }

    private readonly IReadOnlyList<IAIFunctionGroup> _toolProviders;

    public ILLMChatClient ChatClient { get; }

    public MiniSweAgent(ILLMChatClient agent, MiniSweAgentConfig? config = null)
    {
        ChatClient = agent;
        Config = config ?? MiniSweAgentConfigLoader.LoadDefaultWindowsConfig();
        _toolProviders = CreateToolProviders(Config);
    }

    public string Name { get; } = "MiniSWE Agent";

    public async IAsyncEnumerable<ChatCallResult> Execute(ITextDialogSession dialogSession,
        IInvokeInteractor? interactor = null,
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
            SystemPrompt = dialogSession.SystemPrompt,
            SystemTemplate = Config.SystemTemplate,
            InstanceTemplate = Config.InstanceTemplate
        };
        contextBuilder.MapFromRequest(request);
        if (dialogSession is ProjectSessionViewModel projectSession)
        {
            contextBuilder.WorkingDirectory = projectSession.WorkingDirectory;
        }

        if (_toolProviders.Count > 0)
        {
            contextBuilder.FunctionGroups ??= [];
            var functionGroups = contextBuilder.FunctionGroups;
            foreach (var toolProvider in _toolProviders)
            {
                if (!functionGroups.Contains(toolProvider, AIFunctionGroupComparer.Instance))
                {
                    functionGroups.Add(toolProvider);
                }
            }
        }

        if (!Config.UseToolCall)
        {
            contextBuilder.CallEngine = new MiniSWEFunctionCallEngine(Config);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            if (Config.StepLimit > 0 && CallCount >= Config.StepLimit)
            {
                throw new Exception("Step limit exceeded");
            }

            var requestContext = await contextBuilder.BuildAsync(ChatClient.Model, cancellationToken);
            ChatCallResult? callResult = null;
            int retryCount = 0;
            while (retryCount < StepRetryCount)
            {
                callResult = await ChatClient.SendRequest(requestContext, interactor, cancellationToken);
                chatHistory.Add(callResult);
                yield return callResult;
                if (callResult.IsCanceled)
                {
                    yield break;
                }

                if (callResult.IsUnhandledError)
                {
                    yield break;
                }

                if (!callResult.IsInterrupt)
                {
                    break;
                }

                retryCount++;
            }

            CallCount++;

            var lastMessage = chatHistory.LastOrDefault()?.Messages?.LastOrDefault();
            if (IsExitMessage(lastMessage))
            {
                break;
            }
        }
    }

    private static IReadOnlyList<IAIFunctionGroup> CreateToolProviders(MiniSweAgentConfig config)
    {
        var providers = new List<IAIFunctionGroup>();
        var platformId = config.PlatformId?.Trim().ToLowerInvariant();

        switch (platformId)
        {
            case MiniSwePlatforms.Wsl:
            case MiniSwePlatforms.Linux:
                providers.Add(new WslCLIPlugin
                {
                    WslDistributionName = config.WslDistributionName,
                    WslUserName = config.WslUserName,
                    TimeoutSeconds = config.ExecutionTimeout,
                    MapWorkingDirectoryToWsl = config.MapWorkingDirectoryToWsl
                });
                break;
            case MiniSwePlatforms.Windows:
            default:
                providers.Add(new WinCLIPlugin());
                break;
        }

        providers.Add(new FileSystemPlugin());
        return providers;
    }

    private bool IsExitMessage(ChatMessage? message)
    {
        return message?.Text?.Contains(Config.TaskCompleteFlag, StringComparison.Ordinal) == true;
    }
}
