using Azure.AI.Inference;
using Microsoft.Extensions.AI;

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

    public OpenAIO1(AzureEndPoint endpoint, AzureModelInfo modelInfo) : base(endpoint, modelInfo)
    {
    }

    protected override void OnChatCompletionsClientChanged(ChatCompletionsClient client)
    {
        base.OnChatCompletionsClientChanged(client);
        client.UpgradeAPIVersion();
    }

    protected override ChatCompletionsOptions CreateChatOptions()
    {
        new ChatCompletionsOptions()
        {
            Model = this.Id,
            /*AdditionalProperties =
            {
                {
                    "api_version", new BinaryData("2024-12-01-preview")
                }
            }*/
        };
    }
}

public class ChatRequestDeveloperMessage : ChatRequestMessage
{
    public ChatRequestDeveloperMessage()
    {
        
    }
}