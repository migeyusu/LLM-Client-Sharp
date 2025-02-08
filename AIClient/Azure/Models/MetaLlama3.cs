using System.Windows.Documents;
using Azure.AI.Inference;
using Microsoft.Extensions.AI;

namespace LLMClient.Azure.Models;

public class MetaLlama3 : AzureTextModelBase
{
    private float _frequencyPenalty = 0;
    private float _presencePenalty = 0;

    public float PresencePenalty
    {
        get => _presencePenalty;
        set
        {
            if (value.Equals(_presencePenalty)) return;
            _presencePenalty = value;
            OnPropertyChanged();
        }
    }

    public float FrequencyPenalty
    {
        get => _frequencyPenalty;
        set
        {
            if (value.Equals(_frequencyPenalty)) return;
            _frequencyPenalty = value;
            OnPropertyChanged();
            OnPropertyChanged();
        }
    }

    public MetaLlama3(AzureEndPoint endpoint, AzureModelInfo modelInfo) : base(endpoint, modelInfo)
    {
        this.MaxTokens = 2048;
        this.Temperature = 0.8f;
        this.TopP = 0.1f;
    }

    protected override ChatOptions CreateChatOptions(IList<ChatMessage> messages)
    {
        var chatCompletionsOptions = base.CreateChatOptions(messages);
        chatCompletionsOptions.FrequencyPenalty = FrequencyPenalty;
        chatCompletionsOptions.PresencePenalty = PresencePenalty;
        return chatCompletionsOptions;
    }
}