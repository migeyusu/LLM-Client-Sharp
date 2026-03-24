using System.Runtime.CompilerServices;
using LLMClient.Abstraction;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.Project;
using LLMClient.ToolCall.DefaultPlugins;
using Microsoft.Extensions.AI;

namespace LLMClient.Agent.MiniSWE;

/// <summary>
/// Mini-SWE-Agent 核心实现
/// 原汁原味的 ReAct 循环实现
/// </summary>
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

    public MiniSweAgent(
        MiniSweAgentConfig config, ILLMChatClient agent)
    {
        Config = config;
        this.ChatClient = agent;
        _toolProviders = [new WinCLIPlugin(), new FileSystemPlugin()];
    }

    public async IAsyncEnumerable<ChatCallResult> Execute(ITextDialogSession dialogSession,
        IInvokeInteractor? interactor = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chatHistory = dialogSession.GetHistory();
        if (chatHistory[^1] is not IRequestItem request)
        {
            yield break;
        }

        var contextBuilder = new AgentDialogContextBuilder(chatHistory)
        {
            PlatformId = "windows",
            IncludeHistoryMessages = true,
            IncludeRagInstructions = true,
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

        // 3. 主循环
        while (!cancellationToken.IsCancellationRequested)
        {
            // 检查限制
            if (Config.StepLimit > 0 && CallCount >= Config.StepLimit)
            {
                throw new Exception("Step limit exceeded");
            }

            var requestContext = await contextBuilder.BuildAsync(ChatClient.Model, cancellationToken);
            ChatCallResult callResult;
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


            // 检查是否完成
            var lastMessage = chatHistory.LastOrDefault()?.Messages?.LastOrDefault();
            if (IsExitMessage(lastMessage))
            {
                break;
            }
        }
    }


    private bool IsExitMessage(ChatMessage? message)
    {
        // 你可以根据需要定义退出条件
        return message?.Text?.Contains("COMPLETE_TASK_AND_SUBMIT_FINAL_OUTPUT") == true;
    }
}