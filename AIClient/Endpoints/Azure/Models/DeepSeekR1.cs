using Microsoft.Extensions.AI;

namespace LLMClient.Endpoints.Azure.Models;

public class DeepSeekR1 : AzureClientBase
{
    private int _maxTokens = 2048;

    public int MaxTokens
    {
        get => _maxTokens;
        set
        {
            if (value == _maxTokens) return;
            _maxTokens = value;
            OnPropertyChanged();
        }
    }

    public DeepSeekR1(AzureEndPointBase endpoint, AzureModelInfo modelInfo) : base(endpoint, modelInfo)
    {
    }

    protected override ChatOptions CreateChatOptions(IList<ChatMessage> messages)
    {
        var chatCompletionsOptions = base.CreateChatOptions(messages);
        chatCompletionsOptions.MaxOutputTokens = MaxTokens;
        return chatCompletionsOptions;
    }
}