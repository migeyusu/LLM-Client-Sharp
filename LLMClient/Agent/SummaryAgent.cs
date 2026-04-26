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
        if (chatHistory.Count == 0)
        {
            yield break;
        }

        var contextBuilder =
            DefaultRequestContextBuilder.CreateFromHistory(chatHistory, systemPrompt: dialogSession.SystemPrompt);
        var requestContext = await contextBuilder.BuildAsync(ChatClient.Model, cancellationToken);
        await foreach (var step in ChatClient.SendRequestAsync(requestContext, cancellationToken))
        {
            yield return step;
        }
    }
}