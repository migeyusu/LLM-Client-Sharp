using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.ViewModel.Base;

using LLMClient.Dialog;
using LLMClient.Persistance;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Endpoints.OpenAIAPI;

public class APIModelInfo : NotifyDataErrorInfoViewModelBase, IEndpointModel
{
    public string? SeriesName
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string? Provider
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    [JsonPropertyName("Id")]
    public string APIId
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = string.Empty;

    [JsonPropertyName("Name")]
    public string Name
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = string.Empty;

    [JsonIgnore]
    public bool IsNotMatchFromSource
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool Streaming
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = true;

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
            PostOnPropertyChanged();
        }
    }

    public bool UrlIconEnable
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
            _icon = null;
            OnPropertyChanged(nameof(Icon));
        }
    } = false;

    public ModelIconType IconType
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
            _icon = null;
            OnPropertyChanged(nameof(Icon));
        }
    } = ModelIconType.None;

    public string? IconUrl
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
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
        get;
        set
        {
            if (value == field) return;
            field = value;
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

    private ThemedIcon? _icon = null;

    private string? _description;

    public int MaxContextSize
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = 200 * 1000;

    [JsonIgnore] public ILLMAPIEndpoint Endpoint { get; set; } = new EmptyLLMEndpoint();

    public ThinkingIncludeMode ThinkingIncludeMode
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = ThinkingIncludeMode.KeepLast;

    public bool SupportSystemPrompt
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = true;

    public bool TopPEnable
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool TopKEnable
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }


    public bool TemperatureEnable
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool MaxTokensEnable
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = true;

    public bool FrequencyPenaltyEnable
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool PresencePenaltyEnable
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool SeedEnable
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string? SystemPrompt
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public float TopP
    {
        get;
        set
        {
            if (value.Equals(field)) return;
            field = value;
            OnPropertyChanged();
        }
    } = 1;

    public int TopKMax
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = 50;

    public int TopK
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }


    public float Temperature
    {
        get;
        set
        {
            if (value.Equals(field)) return;
            field = value;
            OnPropertyChanged();
        }
    } = 1;

    public int MaxTokens
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = 4 * 1000;

    public int MaxTokenLimit
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = 128 * 1000;

    public bool Reasonable
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
            if (!Reasonable)
            {
                ThinkingEnabled = false;
            }
        }
    } = false;

    public IThinkingConfig? ThinkingConfig
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool ThinkingEnabled
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
            if (ThinkingConfig == null && value)
            {
                ThinkingConfig = new ThinkingConfigViewModel();
            }
        }
    } = true;

    /// <summary>
    /// 许多function call的实现不支持streaming，这个选项用于在function call时禁用streaming
    /// </summary>
    public bool FunctionCallOnStreaming
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = false;

    public ReactHistoryCompressionOptions HistoryCompression
    {
        get;
        set
        {
            if (ReferenceEquals(value, field)) return;
            field = value ?? new ReactHistoryCompressionOptions();
            OnPropertyChanged();
        }
    } = new();

    public bool SupportStreaming
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
            //streaming 将默认随着这个选项变化
            Streaming = value;
        }
    } = true;

    public bool SupportImageGeneration
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool SupportAudioGeneration
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool SupportVideoGeneration
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool SupportSearch
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool SupportFunctionCall
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool SupportAudioInput
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool SupportVideoInput
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool SupportTextGeneration
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = true;

    public bool SupportImageInput
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public IPriceCalculator? PriceCalculator { get; init; } = new TokenBasedPriceCalculator();

    [JsonIgnore] public UsageCounter? Telemetry { get; set; }

    public float FrequencyPenalty
    {
        get;
        set
        {
            if (value.Equals(field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public float PresencePenalty
    {
        get;
        set
        {
            if (value.Equals(field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public long? Seed
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
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
            MessageBoxes.Error("复制失败: " + ex.Message, "错误");
        }
    });
}