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

    public int StepRetryCount { get; set; } = 3;

    public MiniSweAgentConfig Config { get; }

    public ILLMChatClient ChatClient { get; }

    public AgentOption AgentOption { get; }

    public abstract string Name { get; }

    protected ReactAgentBase(ILLMChatClient chatClient, AgentOption agentOption, MiniSweAgentConfig config)
    {
        ChatClient = chatClient;
        AgentOption = agentOption;
        Config = config;
    }

    /// <summary>
    /// Builds the <see cref="RequestContext"/> from the dialog session.
    /// Return <c>null</c> when there is no actionable request.
    /// </summary>
    protected abstract Task<RequestContext?> BuildRequestContextAsync(
        ITextDialogSession dialogSession,
        CancellationToken cancellationToken);

    public async IAsyncEnumerable<ReactStep> Execute(
        ITextDialogSession dialogSession,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // yield return is not allowed inside try/catch; capture the exception first.
        RequestContext? requestContext = null;
        Exception? setupError = null;
        try
        {
            requestContext = await BuildRequestContextAsync(dialogSession, cancellationToken);
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
                    yield return step;
                    if (step.Result != null)
                    {
                        requestContext.ChatMessages.AddRange(step.Result.Messages);
                        lastResult = step.Result;
                    }
                }

                if (lastResult?.IsCanceled == true) yield break;
                if (lastResult?.IsInvalidRequest == true) yield break;
                if (lastResult?.Exception is AgentFlowException) break;
                if (lastResult?.Exception == null) break; // success — do not retry
                retryCount++;
            }

            CallCount++;

            if (IsExitMessage(requestContext.ReadonlyHistory.LastOrDefault()))
                break;
        }
    }

    protected bool IsExitMessage(ChatMessage? message)
    {
        var text = message?.Text;
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains(Config.TaskCompleteFlag, StringComparison.Ordinal);
    }
}

