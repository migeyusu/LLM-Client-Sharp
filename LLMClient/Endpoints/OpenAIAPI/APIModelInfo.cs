using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Data;
using LLMClient.Dialog;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Endpoints.OpenAIAPI;

public class APIModelInfo : NotifyDataErrorInfoViewModelBase, IEndpointModel
{
    public string? OfficialName
    {
        get => _officialName;
        set
        {
            if (value == _officialName) return;
            _officialName = value;
            OnPropertyChanged();
        }
    }

    public string? Publisher
    {
        get => _publisher;
        set
        {
            if (value == _publisher) return;
            _publisher = value;
            OnPropertyChanged();
        }
    }
    
    [JsonPropertyName("Id")]
    public string APIId
    {
        get => _id;
        set
        {
            if (value == _id) return;
            _id = value;
            OnPropertyChanged();
        }
    }

    [JsonPropertyName("Name")]
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
    public bool IsNotMatchFromSource
    {
        get => _isNotMatchFromSource;
        set
        {
            if (value == _isNotMatchFromSource) return;
            _isNotMatchFromSource = value;
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
        get
        {
            if (_icon == null)
            {
                if (UrlIconEnable && !string.IsNullOrEmpty(IconUrl))
                {
                    _icon = AsyncThemedIcon.FromUri(new Uri(this.IconUrl));
                }
                else if (!UrlIconEnable)
                {
                    _icon = ImageExtensions.GetIcon(this.IconType);
                }
            }

            return _icon ?? ImageExtensions.APIThemedIcon;
        }
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
            _icon = null;
            OnPropertyChanged(nameof(Icon));
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
            _icon = null;
            OnPropertyChanged(nameof(Icon));
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
            _icon = null;
            OnPropertyChanged(nameof(Icon));
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
    private bool _supportsystemPrompt = true;
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
    private bool _isNotMatchFromSource;
    private bool _supportTextGeneration = true;
    private bool _supportVideoInput;
    private bool _supportAudioInput;
    private bool _supportFunctionCall;
    private bool _supportSearch;
    private bool _supportImageGeneration;
    private bool _reasonable = false;
    private bool _supportImageInput;
    private bool _supportVideoGeneration;
    private bool _supportAudioGeneration;
    private bool _supportStreaming = true;
    private bool _functionCallOnStreaming = false;
    private bool _thinkingEnabled;
    private IThinkingConfig? _thinkingConfig;
    private ThinkingIncludeMode _thinkingIncludeMode;
    private string? _officialName;
    private string? _publisher;

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

    [JsonIgnore] public ILLMAPIEndpoint Endpoint { get; set; } = new EmptyLLMEndpoint();

    public ThinkingIncludeMode ThinkingIncludeMode
    {
        get => _thinkingIncludeMode;
        set
        {
            if (value == _thinkingIncludeMode) return;
            _thinkingIncludeMode = value;
            OnPropertyChanged();
        }
    }

    public bool SupportSystemPrompt
    {
        get => _supportsystemPrompt;
        set
        {
            if (value == _supportsystemPrompt) return;
            _supportsystemPrompt = value;
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
            if (!Reasonable)
            {
                ThinkingEnabled = false;
            }
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
            if (ThinkingConfig == null && value)
            {
                ThinkingConfig = new ThinkingConfigViewModel();
            }
        }
    }

    /// <summary>
    /// 许多function call的实现不支持streaming，这个选项用于在function call时禁用streaming
    /// </summary>
    public bool FunctionCallOnStreaming
    {
        get => _functionCallOnStreaming;
        set
        {
            if (value == _functionCallOnStreaming) return;
            _functionCallOnStreaming = value;
            OnPropertyChanged();
        }
    }

    public bool SupportStreaming
    {
        get => _supportStreaming;
        set
        {
            if (value == _supportStreaming) return;
            _supportStreaming = value;
            OnPropertyChanged();
            //streaming 将默认随着这个选项变化
            Streaming = value;
        }
    }

    public bool SupportImageGeneration
    {
        get => _supportImageGeneration;
        set
        {
            if (value == _supportImageGeneration) return;
            _supportImageGeneration = value;
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
        get => _supportVideoGeneration;
        set
        {
            if (value == _supportVideoGeneration) return;
            _supportVideoGeneration = value;
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

    public IPriceCalculator? PriceCalculator { get; init; } = new TokenBasedPriceCalculator();

    [JsonIgnore] public UsageCount? Telemetry { get; set; }

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

    public ICommand CopyCommand => new ActionCommand(o =>
    {
        try
        {
            APIEndPoint.CopyToClipboard(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show("复制失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    });
}