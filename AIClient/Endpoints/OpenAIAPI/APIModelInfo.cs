﻿using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Media;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.UI;
using LLMClient.UI.Component;

namespace LLMClient.Endpoints.OpenAIAPI;

public class APIModelInfo : NotifyDataErrorInfoViewModelBase, ILLMModel
{
    public string Id
    {
        get => _id;
        set
        {
            if (value == _id) return;
            _id = value;
            OnPropertyChanged();
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            if (value == _name) return;
            _name = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public bool IsNotAvailable
    {
        get => _isNotAvailable;
        set
        {
            if (value == _isNotAvailable) return;
            _isNotAvailable = value;
            OnPropertyChanged();
        }
    }

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

    [JsonIgnore]
    public ThemedIcon Icon
    {
        get { return _icon ?? ImageExtensions.APIIcon; }
        private set
        {
            if (Equals(value, _icon)) return;
            _icon = value;
            OnPropertyChangedAsync();
        }
    }

    public bool UrlIconEnable
    {
        get => _urlIconEnable;
        set
        {
            if (value == _urlIconEnable) return;
            _urlIconEnable = value;
            OnPropertyChanged();
            UpdateIcon();
        }
    }

    public ModelIconType IconType
    {
        get => _iconType;
        set
        {
            if (value == _iconType) return;
            _iconType = value;
            OnPropertyChanged();
            UpdateIcon();
        }
    }

    public string? IconUrl
    {
        get => _iconUrl;
        set
        {
            if (value == _iconUrl) return;
            _iconUrl = value;
            OnPropertyChanged();
            UpdateIcon();
        }
    }

    [JsonIgnore]
    public Uri InfoUri
    {
        get { return _description == null ? new Uri("about:blank") : new Uri(_description); }
    }

    public string? InfoUrl
    {
        get => _infoUrl;
        set
        {
            if (value == _infoUrl) return;
            _infoUrl = value;
            OnPropertyChanged();
        }
    }

    public string? Description
    {
        get => _description;
        set
        {
            if (value == _description) return;
            _description = value;
            OnPropertyChanged();
        }
    }

    private string? _iconUrl;
    private ThemedIcon? _icon = null;
    private bool _urlIconEnable = false;
    private ModelIconType _iconType = ModelIconType.None;
    private bool _systemPromptEnable = true;
    private bool _topPEnable;
    private bool _topKEnable;
    private bool _temperatureEnable;
    private bool _maxTokensEnable = true;
    private bool _frequencyPenaltyEnable;
    private bool _presencePenaltyEnable;
    private bool _seedEnable;
    private string? _systemPrompt;

    private float _topP = 1;

    private int _topK;
    private float _frequencyPenalty;
    private float _presencePenalty;
    private long? _seed;
    private float _temperature = 1;
    private int _maxTokens = 4 * 1000;
    private int _maxTokenLimit = 128 * 1000;
    private int _topKMax = 50;
    private string? _description;
    private int _maxContextSize = 200 * 1000;
    private bool _streaming = true;
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string? _infoUrl;
    private bool _isNotAvailable;
    private bool _supportTextGeneration;
    private bool _supportVideoInput;
    private bool _supportAudioInput;
    private bool _supportFunctionCall;
    private bool _supportSearch;
    private bool _supportImageOutput;
    private bool _reasonable = false;
    private bool _supportImageInput;
    private bool _supoortVideoGeneration;
    private bool _supportAudioGeneration;

    public int MaxContextSize
    {
        get => _maxContextSize;
        set
        {
            if (value == _maxContextSize) return;
            _maxContextSize = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore] public ILLMEndpoint Endpoint { get; set; } = new NullLLMEndpoint();

    public bool SystemPromptEnable
    {
        get => _systemPromptEnable;
        set
        {
            if (value == _systemPromptEnable) return;
            _systemPromptEnable = value;
            OnPropertyChanged();
        }
    }

    public bool TopPEnable
    {
        get => _topPEnable;
        set
        {
            if (value == _topPEnable) return;
            _topPEnable = value;
            OnPropertyChanged();
        }
    }

    public bool TopKEnable
    {
        get => _topKEnable;
        set
        {
            if (value == _topKEnable) return;
            _topKEnable = value;
            OnPropertyChanged();
        }
    }


    public bool TemperatureEnable
    {
        get => _temperatureEnable;
        set
        {
            if (value == _temperatureEnable) return;
            _temperatureEnable = value;
            OnPropertyChanged();
        }
    }

    public bool MaxTokensEnable
    {
        get => _maxTokensEnable;
        set
        {
            if (value == _maxTokensEnable) return;
            _maxTokensEnable = value;
            OnPropertyChanged();
        }
    }

    public bool FrequencyPenaltyEnable
    {
        get => _frequencyPenaltyEnable;
        set
        {
            if (value == _frequencyPenaltyEnable) return;
            _frequencyPenaltyEnable = value;
            OnPropertyChanged();
        }
    }

    public bool PresencePenaltyEnable
    {
        get => _presencePenaltyEnable;
        set
        {
            if (value == _presencePenaltyEnable) return;
            _presencePenaltyEnable = value;
            OnPropertyChanged();
        }
    }

    public bool SeedEnable
    {
        get => _seedEnable;
        set
        {
            if (value == _seedEnable) return;
            _seedEnable = value;
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

    public int TopKMax
    {
        get => _topKMax;
        set
        {
            if (value == _topKMax) return;
            _topKMax = value;
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

    public int MaxTokenLimit
    {
        get => _maxTokenLimit;
        set
        {
            if (value == _maxTokenLimit) return;
            _maxTokenLimit = value;
            OnPropertyChanged();
        }
    }

    public bool Reasonable
    {
        get => _reasonable;
        set
        {
            if (value == _reasonable) return;
            _reasonable = value;
            OnPropertyChanged();
        }
    }

    public bool SupportImageGeneration
    {
        get => _supportImageOutput;
        set
        {
            if (value == _supportImageOutput) return;
            _supportImageOutput = value;
            OnPropertyChanged();
        }
    }

    public bool SupportAudioGeneration
    {
        get => _supportAudioGeneration;
        set
        {
            if (value == _supportAudioGeneration) return;
            _supportAudioGeneration = value;
            OnPropertyChanged();
        }
    }

    public bool SupportVideoGeneration
    {
        get => _supoortVideoGeneration;
        set
        {
            if (value == _supoortVideoGeneration) return;
            _supoortVideoGeneration = value;
            OnPropertyChanged();
        }
    }

    public bool SupportSearch
    {
        get => _supportSearch;
        set
        {
            if (value == _supportSearch) return;
            _supportSearch = value;
            OnPropertyChanged();
        }
    }

    public bool SupportFunctionCall
    {
        get => _supportFunctionCall;
        set
        {
            if (value == _supportFunctionCall) return;
            _supportFunctionCall = value;
            OnPropertyChanged();
        }
    }

    public bool SupportAudioInput
    {
        get => _supportAudioInput;
        set
        {
            if (value == _supportAudioInput) return;
            _supportAudioInput = value;
            OnPropertyChanged();
        }
    }

    public bool SupportVideoInput
    {
        get => _supportVideoInput;
        set
        {
            if (value == _supportVideoInput) return;
            _supportVideoInput = value;
            OnPropertyChanged();
        }
    }

    public bool SupportTextGeneration
    {
        get => _supportTextGeneration;
        set
        {
            if (value == _supportTextGeneration) return;
            _supportTextGeneration = value;
            OnPropertyChanged();
        }
    }

    public bool SupportImageInput
    {
        get => _supportImageInput;
        set
        {
            if (value == _supportImageInput) return;
            _supportImageInput = value;
            OnPropertyChanged();
        }
    }

    public IPriceCalculator? PriceCalculator { get; set; } = new TokenBasedPriceCalculator();

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

    private void UpdateIcon()
    {
        if (UrlIconEnable)
        {
            if (!string.IsNullOrEmpty(IconUrl))
            {
                this.Icon = new AsyncThemedIcon(((async () =>
                {
                    var imageSource = await new Uri(this.IconUrl).GetIcon();
                    if (imageSource != null)
                        return imageSource;
                    return ImageExtensions.APIIcon.CurrentSource;
                })), null);
            }
        }
        else
        {
            this.Icon = ImageExtensions.GetIcon(this.IconType);
        }
    }
}