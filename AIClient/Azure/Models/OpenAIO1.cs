using System.ClientModel;
using Azure;
using Azure.AI.TextAnalytics;
using Microsoft.Extensions.AI;
using OpenAI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace LLMClient.Azure.Models;

public class OpenAIO1 : AzureModelBase
{
    private string _systemPrompt = string.Empty;

    public string SystemPrompt
    {
        get => _systemPrompt;
        set
        {
            if (value == _systemPrompt) return;
            _systemPrompt = value;
            OnPropertyChanged();
        }
    }

    public OpenAIO1(GithubCopilotEndPoint endpoint, AzureModelInfo modelInfo) : base(endpoint, modelInfo)
    {
    }

    public override IChatClient CreateClient(GithubCopilotEndPoint endpoint)
    {
        var openAiClient = new OpenAIClient(new ApiKeyCredential(endpoint.APIToken),
            new OpenAIClientOptions() { Endpoint = new Uri(endpoint.URL) });
        return new OpenAIChatClient(openAiClient, this.Id);
    }

    protected override ChatOptions CreateChatOptions(IList<ChatMessage> messages)
    {
        messages.Add(new ChatMessage(new ChatRole("developer"), string.Empty));
        return new ChatOptions()
        {
            ModelId = this.Id,
        };
    }
}