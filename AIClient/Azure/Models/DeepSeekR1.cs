using Azure.AI.Inference;
using Microsoft.Extensions.AI;

namespace LLMClient.Azure.Models;

public class DeepSeekR1 : AzureModelBase
{
    private int _maxTokens;

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

    public DeepSeekR1(AzureEndPoint endpoint, AzureModelInfo modelInfo) : base(endpoint, modelInfo)
    {
    }

    protected override ChatCompletionsOptions CreateChatOptions()
    {
        var chatCompletionsOptions = base.CreateChatOptions();
        chatCompletionsOptions.MaxTokens = MaxTokens;
        return chatCompletionsOptions;
    }
}