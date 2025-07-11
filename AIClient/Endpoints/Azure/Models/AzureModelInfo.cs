using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows.Documents;
using System.Windows.Media;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Render;
using LLMClient.UI.Component;

namespace LLMClient.Endpoints.Azure.Models;

public class AzureModelInfo : ILLMModel
{
    [JsonPropertyName("friendly_name")] public string FriendlyName { get; set; } = string.Empty;

    [JsonIgnore]
    public string Id
    {
        get { return this.Publisher?.ToLower() + "/" + this.OriginalName; }
    }

    [JsonPropertyName("name")] public string ModelName { get; set; } = string.Empty;

    [JsonIgnore]
    public string Name
    {
        get { return FriendlyName; }
    }

    [JsonIgnore] public bool Streaming { get; set; } = true;

    private ThemedIcon? _themedIcon;

    [JsonIgnore]
    public ThemedIcon Icon
    {
        get
        {
            if (_themedIcon != null)
            {
                return _themedIcon;
            }

            _themedIcon = new AsyncThemedIcon((() =>
                {
                    return Task.Run((() =>
                    {
                        ImageSource? lightModeIconBrush = null;
                        if (!string.IsNullOrEmpty(LightModeIconString))
                        {
                            lightModeIconBrush = Extension.LoadSvgFromBase64(LightModeIconString);
                        }
                        else if (LogoUrl != null)
                        {
                            var requestUri = new Uri($"https://github.com{LogoUrl}");
                            lightModeIconBrush = requestUri.GetIcon().Result;
                        }

                        return lightModeIconBrush ?? ImageExtensions.APIIcon.CurrentSource;
                    }));
                }),
                string.IsNullOrEmpty(DarkModeIconString)
                    ? null
                    : (() => { return Task.Run((() => Extension.LoadSvgFromBase64(DarkModeIconString))); }));
            return _themedIcon;
        }
    }

    [JsonIgnore] public int MaxContextSize { get; set; }

    [JsonPropertyName("max_input_tokens")] public ulong? MaxInputTokens { get; set; }

    [JsonPropertyName("max_output_tokens")]
    public ulong? MaxOutputTokens { get; set; }

    [JsonPropertyName("original_name")] public string OriginalName { get; set; } = string.Empty;

    [JsonPropertyName("model_family")] public string? ModelFamily { get; set; }

    [JsonPropertyName("description")] public string? DescriptionRaw { get; set; }

    [JsonIgnore] public ILLMEndpoint Endpoint { get; set; } = NullLLMEndpoint.Instance;

    [JsonIgnore] public bool SystemPromptEnable { get; set; } = true;

    [JsonIgnore] public bool TopPEnable { get; set; }

    [JsonIgnore] public bool TopKEnable { get; set; }

    [JsonIgnore] public bool TemperatureEnable { get; set; }

    [JsonIgnore] public bool MaxTokensEnable { get; set; } = true;

    [JsonIgnore] public bool FrequencyPenaltyEnable { get; set; }

    [JsonIgnore] public bool PresencePenaltyEnable { get; set; }

    [JsonIgnore] public bool SeedEnable { get; set; }

    [JsonIgnore] public string? SystemPrompt { get; set; }

    [JsonIgnore]
    public float TopP
    {
        get => _topP;
        set
        {
            _topP = value;
            this.TopPEnable = true;
        }
    }

    [JsonIgnore] public int TopKMax { get; set; }

    [JsonIgnore]
    public int TopK
    {
        get => _topK;
        set
        {
            _topK = value;
            TopKEnable = true;
        }
    }

    [JsonIgnore]
    public float Temperature
    {
        get => _temperature;
        set
        {
            _temperature = value;
            TemperatureEnable = true;
        }
    }

    [JsonIgnore] public int MaxTokens { get; set; } = 4096;

    [JsonIgnore]
    public int MaxTokenLimit
    {
        get => MaxOutputTokens.HasValue ? (int)MaxOutputTokens.Value : 4096;
        set => throw new NotSupportedException("MaxTokenLimit is read-only and should not be set directly.");
    }

    public bool Reasonable
    {
        get { return Tags?.Contains("reasoning") == true; }
    }

    public bool SupportTextGeneration
    {
        get { return SupportedOutputModalities?.Contains("text") == true; }
    }

    public bool SupportImageGeneration => false;

    public bool SupportAudioGeneration { get; } = false;

    public bool SupportVideoGeneration { get; } = false;

    public bool SupportSearch { get; } = false;

    public bool SupportFunctionCall
    {
        get { return Tags?.Contains("agents") == true; }
    }

    public bool SupportAudioInput { get; } = false;

    public bool SupportVideoInput { get; } = false;

    public bool SupportImageInput
    {
        get { return SupportedInputModalities?.Contains("image") == true; }
    }

    public IPriceCalculator? PriceCalculator { get; } = null;

    [JsonIgnore]
    public float FrequencyPenalty
    {
        get => _frequencyPenalty;
        set
        {
            _frequencyPenalty = value;
            FrequencyPenaltyEnable = true;
        }
    }

    [JsonIgnore]
    public float PresencePenalty
    {
        get => _presencePenalty;
        set
        {
            _presencePenalty = value;
            PresencePenaltyEnable = true;
        }
    }

    [JsonIgnore] public long? Seed { get; set; }

    private FlowDocument? _description;

    [JsonIgnore]
    public FlowDocument? Description
    {
        get
        {
            if (DescriptionRaw == null)
            {
                return null;
            }

            if (_description == null)
            {
                _description = DescriptionRaw.ToFlowDocument();
            }

            return _description;
        }
    }

    [JsonPropertyName("publisher")] public string? Publisher { get; set; }

    [JsonPropertyName("logo_url")] public string? LogoUrl { get; set; }

    [JsonPropertyName("notes")] public string? NotesRaw { get; set; }

    private FlowDocument? _notes;

    [JsonIgnore]
    public FlowDocument? Notes
    {
        get
        {
            if (NotesRaw == null)
            {
                return null;
            }

            if (_notes == null)
            {
                _notes = NotesRaw.ToFlowDocument();
            }

            return _notes;
        }
    }

    [JsonPropertyName("evaluation")] public string? EvaluationRaw { get; set; }

    private FlowDocument? _evaluation;

    [JsonIgnore]
    public FlowDocument? Evaluation
    {
        get
        {
            if (EvaluationRaw == null)
            {
                return null;
            }

            if (_evaluation == null)
            {
                _evaluation = EvaluationRaw.ToFlowDocument();
            }

            return _evaluation;
        }
    }

    [JsonPropertyName("dark_mode_icon")] public string? DarkModeIconString { get; set; }

    [JsonPropertyName("light_mode_icon")] public string? LightModeIconString { get; set; }

    private float _topP;
    private float _frequencyPenalty = 0;
    private float _presencePenalty = 0;
    private float _temperature = 1f;
    private int _topK;

    [JsonPropertyName("training_data_date")]
    public string? TrainingDataDate { get; set; }

    [JsonPropertyName("summary")] public string? Summary { get; set; }

    [JsonPropertyName("model_version")] public string? ModelVersion { get; set; }

    public const string FilteredTask = "chat-completion";
    [JsonPropertyName("task")] public string? ModelTask { get; set; }

    /// <summary>
    /// "multimodal","reasoning","conversation", "multipurpose","multilingual","coding","rag",
    /// "agents","understanding","low latency","large context", "vision","audio","summarization"
    /// </summary>
    [JsonPropertyName("tags")]
    public string[]? Tags { get; set; }

    public const string FilteredInputText = "text";

    [JsonPropertyName("supported_input_modalities")]
    public string[]? SupportedInputModalities { get; set; }

    [JsonPropertyName("supported_output_modalities")]
    public string[]? SupportedOutputModalities { get; set; }

    [JsonPropertyName("capabilities")] public JsonObject? Capabilities { get; set; }
}