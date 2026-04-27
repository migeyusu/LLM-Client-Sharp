using System.Runtime.CompilerServices;
using LLMClient.Abstraction;
using LLMClient.Agent.MiniSWE;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using Microsoft.Extensions.AI;

namespace LLMClient.Agent;

/// <summary>
/// Abstract base for agents that execute a ReAct (Reasoning + Acting) loop.
/// Subclasses implement <see cref="BuildRequestContextAsync"/> to configure tools and prompts.
/// The loop itself is identical for all agents: send request, accumulate messages, check exit.
/// </summary>
public abstract class ReactAgentBase : ISingleClientAgent
{
    public int CallCount { get; set; }

    public MiniSweAgentConfig Config { get; }

    public ILLMChatClient ChatClient { get; }

    public AgentConfig AgentConfig { get; }

    public abstract string Name { get; }

    /// <summary>
    /// 用于 ReAct 历史轮次隔离的 Agent 标识。默认使用 <see cref="Name">。
    /// </summary>
    protected virtual string AgentId => Name;

    private string? _previousAssistantText;

    protected ReactAgentBase(ILLMChatClient chatClient, AgentConfig agentConfig, MiniSweAgentConfig config)
    {
        ChatClient = chatClient;
        AgentConfig = agentConfig;
        Config = config;
    }

    /// <summary>
    /// Builds the <see cref="RequestContext"/> from the dialog session.
    /// Return <c>null</c> when there is no actionable request.
    /// </summary>
    protected abstract Task<RequestContext?> BuildRequestContextAsync(
        ISession dialogSession, AgentRunOption option,
        CancellationToken cancellationToken);

    public async IAsyncEnumerable<ReactStep> Execute(ISession dialogSession,
        AgentRunOption option,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // yield return is not allowed inside try/catch; capture the exception first.
        RequestContext? requestContext = null;
        Exception? setupError = null;
        try
        {
            requestContext = await BuildRequestContextAsync(dialogSession, option, cancellationToken);
        }
        catch (Exception ex)
        {
            setupError = ex;
        }

        if (setupError != null)
        {
            var errorStep = new ReactStep();
            errorStep.CompleteWithError(setupError);
            yield return errorStep;
            yield break;
        }

        if (requestContext == null)
            yield break;

        // 注入 AgentId，确保该 Agent 的 ReAct 轮次与其他 Agent 隔离
        _previousAssistantText = null;

        // 通过 exit 回调将 IsExitMessage 逻辑下推到 IReactClient 的 ReAct 循环中，
        // 避免外部循环调用 SendRequestAsync 导致上下文异常
        // 注意：不检查 IsCompleted / Exception，这些由 IReactClient 内部的 DefaultExit 处理。
        // 如果 Agent 遇到异常（如 HTTP 错误），ReactClientBase 的 ProduceStepAsync 会设置
        // stepResult.IsCompleted = false，exit 回调会继续循环触发重试。
        Predicate<ReactStep> exit = step =>
        {
            var result = step.Result;

            // Agent 级自定义退出条件：IsExitMessage 检测任务完成标志
            var lastAssistant = result?.Messages
                .LastOrDefault(m => m.Role == ChatRole.Assistant);
            if (IsExitMessage(lastAssistant))
                return true;

            // 步数限制检查
            CallCount++;
            if (Config.StepLimit > 0 && CallCount >= Config.StepLimit)
                throw new StepOverflowException();

            return false;
        };

        await foreach (var step in ChatClient.SendRequestAsync(requestContext, exit, cancellationToken))
        {
            yield return step;
            // 累积消息到请求上下文，确保后续 SendRequestAsync 调用能看到历史消息
            if (step.Result != null)
                requestContext.ChatMessages.AddRange(step.Result.Messages);
        }
    }

    protected virtual bool IsExitMessage(ChatMessage? message)
    {
        var text = message?.Text;
        if (!string.IsNullOrEmpty(text))
        {
            if (text.Contains(Config.TaskCompleteFlag, StringComparison.Ordinal))
                return true;

            if (text == _previousAssistantText)
                return true;
        }

        _previousAssistantText = text;
        return false;
    }
}