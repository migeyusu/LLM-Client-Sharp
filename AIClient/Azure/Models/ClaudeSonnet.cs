using Microsoft.Extensions.AI;

namespace LLMClient.Azure.Models;

public class ClaudeSonnet : AzureModelBase
{
    public ClaudeSonnet(GithubCopilotEndPoint endpoint, AzureModelInfo modelInfo) : base(endpoint, modelInfo)
    {
    }

    public override IChatClient CreateClient(GithubCopilotEndPoint endpoint)
    {
        return base.CreateClient(endpoint);
    }

    protected override ChatOptions CreateChatOptions(IList<ChatMessage> messages)
    {
        return base.CreateChatOptions(messages);
    }
}