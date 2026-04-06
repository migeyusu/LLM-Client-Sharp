using System.ComponentModel;
using System.Runtime.CompilerServices;
using LLMClient.Abstraction;
using LLMClient.Dialog.Models;

namespace LLMClient.Agent;

[Description("Summary Agent")]
public class SummaryAgent : IAgent
{
    public SummaryAgent(ILLMChatClient chatClient)
    {
        ChatClient = chatClient;
    }

    public string Name { get; } = "Summary Agent";

    public ILLMChatClient ChatClient { get; }

    public async IAsyncEnumerable<ReactStep> Execute(ITextDialogSession dialogSession,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chatHistory = dialogSession.GetHistory();
        if (chatHistory.Count == 0 || chatHistory[^1] is not IRequestItem request)
        {
            yield break;
        }

        var contextBuilder = DefaultDialogContextBuilder.CreateFromHistory(chatHistory, dialogSession.SystemPrompt);
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
        return lastResult is { Exception: null, Messages.Count: > 0 };
    }
}

