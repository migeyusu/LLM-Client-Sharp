using LLMClient.Abstraction;
using LLMClient.ContextEngineering.PromptGeneration;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
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

    /// <summary>
    /// 额外的模板变量
    /// </summary>
    public Dictionary<string, object?> ExtraTemplateVars { get; } = new();

    public ILLMChatClient ChatClient { get; }

    public MiniSweAgent(
        MiniSweAgentConfig config, ILLMChatClient agent)
    {
        Config = config;
        this.ChatClient = agent;
        _toolProviders = [new WinCLIPlugin(), new FileSystemPlugin()];
    }

    /// <summary>
    /// 获取所有模板变量的合并结果
    /// 对应 Python 的 get_template_vars 方法
    /// </summary>
    public Dictionary<string, object?> GetTemplateVars(Dictionary<string, object?>? extra = null)
    {
        var result = new Dictionary<string, object?>();

        // 1. 配置变量
        result["step_limit"] = Config.StepLimit;
        // 2. 环境变量 (系统信息)
        result["system"] = Environment.OSVersion.Platform.ToString();
        result["release"] = Environment.OSVersion.Version.ToString();
        result["version"] = Environment.OSVersion.VersionString;
        result["machine"] = Environment.MachineName;

        // 4. 额外变量
        foreach (var kvp in ExtraTemplateVars)
        {
            result[kvp.Key] = kvp.Value;
        }

        // 5. 传入的额外变量（优先级最高）
        if (extra != null)
        {
            foreach (var kvp in extra)
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }

    /// <summary>
    /// 渲染 Handlebars 模板
    /// </summary>
    private async Task<string> RenderTemplateAsync(string template, Dictionary<string, object?>? extraVars = null)
    {
        var variables = GetTemplateVars(extraVars);
        return await PromptTemplateRenderer.RenderHandlebarsAsync(template, variables);
    }

    public async IAsyncEnumerable<ChatCallResult> Execute(DialogContext rawContext,
        IInvokeInteractor? interactor = null,
        CancellationToken cancellationToken = default)
    {
        // 初始化
        ExtraTemplateVars["task"] =
            rawContext.UserPrompt ?? throw new NotSupportedException("User prompt cannot be null");
        if (!string.IsNullOrEmpty(rawContext.WorkingDirectory))
        {
            ExtraTemplateVars["cwd"] = rawContext.WorkingDirectory;
        }

        // 1. 添加系统消息
        var systemContent = await RenderTemplateAsync(Config.SystemTemplate);
        systemContent = rawContext.SystemPrompt + "\r\n" + systemContent;

        // 2. 添加实例消息（任务描述）
        var instanceContent = await RenderTemplateAsync(Config.InstanceTemplate);
        var chatHistory = rawContext.DialogItems.ToList();
        chatHistory[^1] = new RequestViewItem(instanceContent);
        var dialogContext = new DialogContext(chatHistory) { SystemPrompt = systemContent };
        dialogContext.MapFromRequest(rawContext);

        if (_toolProviders.Count > 0)
        {
            dialogContext.FunctionGroups ??= [];
            dialogContext.FunctionGroups.AddRange(_toolProviders);
        }

        if (!Config.UseToolCall)
        {
            dialogContext.CallEngine = new MiniSWEFunctionCallEngine(Config);
        }

        // 3. 主循环
        while (!cancellationToken.IsCancellationRequested)
        {
            // 检查限制
            if (Config.StepLimit > 0 && CallCount >= Config.StepLimit)
            {
                throw new Exception("Step limit exceeded");
            }

            ChatCallResult callResult;
            int retryCount = 0;
            while (retryCount < StepRetryCount)
            {
                callResult = await ChatClient.SendRequest(dialogContext, null, cancellationToken);
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