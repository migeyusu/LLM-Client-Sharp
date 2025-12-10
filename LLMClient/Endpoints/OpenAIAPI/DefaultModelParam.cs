using LLMClient.Abstraction;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Dialog;

namespace LLMClient.Endpoints.OpenAIAPI;

public class DefaultModelParam : BaseViewModel, IModelParams
{
    private string? _systemPrompt;
    private float _topP;
    private int _topK;
    private float _temperature;
    private int _maxTokens;
    private float _frequencyPenalty;
    private float _presencePenalty;
    private long? _seed;
    private bool _streaming = true;
    private IThinkingConfig? _thinkingConfig;
    private bool _thinkingEnabled;

    public bool Streaming
    {
        get => _streaming;
        set
        {
            if (value == _streaming) return;
            _streaming = value;
            OnPropertyChanged();
        }
    }

    public string? SystemPrompt
    {
        get => _systemPrompt;
        set
        {
            if (value == _systemPrompt) return;
            _systemPrompt = value;
            OnPropertyChanged();
            OnPropertyChanged();
        }
    }

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

    public int TopK
    {
        get => _topK;
        set
        {
            if (value == _topK) return;
            _topK = value;
            OnPropertyChanged();
        }
    }

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

    public float FrequencyPenalty
    {
        get => _frequencyPenalty;
        set
        {
            if (value.Equals(_frequencyPenalty)) return;
            _frequencyPenalty = value;
            OnPropertyChanged();
        }
    }

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

    public long? Seed
    {
        get => _seed;
        set
        {
            if (value == _seed) return;
            _seed = value;
            OnPropertyChanged();
        }
    }

    public IThinkingConfig? ThinkingConfig
    {
        get => _thinkingConfig;
        set
        {
            if (Equals(value, _thinkingConfig)) return;
            _thinkingConfig = value;
            OnPropertyChanged();
        }
    }

    public bool ThinkingEnabled
    {
        get => _thinkingEnabled;
        set
        {
            if (value == _thinkingEnabled) return;
            _thinkingEnabled = value;
            OnPropertyChanged();
            if (value && ThinkingConfig == null)
            {
                ThinkingConfig = new ThinkingConfigViewModel();
            }
        }
    }
}