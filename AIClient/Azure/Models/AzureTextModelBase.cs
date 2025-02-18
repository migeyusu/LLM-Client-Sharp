using Azure.AI.Inference;
using Microsoft.Extensions.AI;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace LLMClient.Azure.Models;

/// <summary>
/// 文本模型
/// </summary>
public class AzureTextModelBase : AzureModelBase
{
    public string? SystemPrompt
    {
        get => _systemPrompt;
        set
        {
            if (value == _systemPrompt) return;
            _systemPrompt = value;
            OnPropertyChanged();
        }
    }

    private float _topP = 1;
    private float _temperature = 1;
    private int _maxTokens = 4096;
    private string? _systemPrompt;

    /// <summary>
    /// 通过选择最可能的单词来控制文本多样性，直到达到规定的概率。
    /// </summary>
    public float TopP
    {
        get => _topP;
        set
        {
            if (value.Equals(_topP)) return;
            _topP = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 控制响应中的随机性，使用较低的值以获得更确定性。
    /// </summary>
    public float Temperature
    {
        get => _temperature;
        set
        {
            if (value.Equals(_temperature)) return;
            _temperature = value;
            OnPropertyChanged();
        }
    }

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

    protected override ChatOptions CreateChatOptions(IList<ChatMessage> messages)
    {
        var options = new ChatOptions()
        {
            ModelId = this.Id,
            Temperature = this.Temperature,
            MaxOutputTokens = this.MaxTokens,
            TopP = this.TopP
        };
        if (!string.IsNullOrEmpty(SystemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, SystemPrompt));
        }

        return options;
    }

    public AzureTextModelBase(GithubCopilotEndPoint endpoint, AzureModelInfo modelInfo) : base(endpoint, modelInfo)
    {
    }
}