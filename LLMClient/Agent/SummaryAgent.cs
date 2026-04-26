using System.ComponentModel;
using System.Runtime.CompilerServices;
using LLMClient.Abstraction;
using LLMClient.Dialog.Models;

namespace LLMClient.Agent;

[Description("Summary Agent")]
public class SummaryAgent : ISingleClientAgent
{
    public SummaryAgent(ILLMChatClient chatClient)
    {
        ChatClient = chatClient;
    }

    public string Name { get; } = "Summary Agent";

    public ILLMChatClient ChatClient { get; }

    public async IAsyncEnumerable<ReactStep> Execute(IDialogSession dialogSession,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chatHistory = dialogSession.GetChatHistory().ToList();
        if (chatHistory.Count == 0 || chatHistory[^1] is not IRequestItem request)
        {
            yield break;
        }

        var contextBuilder =
            DefaultRequestContextBuilder.CreateFromHistory(chatHistory, systemPrompt: dialogSession.SystemPrompt);
        var requestContext = await contextBuilder.BuildAsync(ChatClient.Model, cancellationToken);
        StepResult? lastResult = null;
        await foreach (var step in ChatClient.SendRequestAsync(requestContext, cancellationToken))
        {
            yield return step;
            if (step.Result != null)
            {
                lastResult = step.Result;
            }
        }

        if (ShouldCompactContext(lastResult))
        {
            await dialogSession.CutContextAsync(request);
        }
    }

    private static bool ShouldCompactContext(StepResult? lastResult)
    {
        if (lastResult == null)
        {
            return false;
        }

        if (lastResult.Exception != null)
        {
            return false;
        }

        if (!lastResult.Messages.Any())
        {
            return false;
        }

        return true;
    }
}