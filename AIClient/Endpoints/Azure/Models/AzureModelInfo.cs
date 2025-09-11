using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Documents;
using System.Windows.Media;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.UI.Component;
using LLMClient.UI.Render;

namespace LLMClient.Endpoints.Azure.Models;

//https://github.com/models/available

public class AzureModelInfo : ILLMChatModel
{
    [JsonIgnore] public bool IsEnabled { get; set; }

    [JsonPropertyName("friendly_name")] public string FriendlyName { get; set; } = string.Empty;

    [JsonIgnore]
    public string Id
    {
        get { return this.Publisher?.ToLower() + "/" + this.OriginalName; }
    }

    [JsonPropertyName("name")] public string ModelName { get; set; } = string.Empty;

    [JsonPropertyName("registry")] public string? Registry { get; set; }

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
                            lightModeIconBrush = ImageExtensions.LoadSvgFromBase64(LightModeIconString);
                        }
                        else if (LogoUrl != null)
                        {
                            var requestUri = new Uri($"https://github.com{LogoUrl}");
                            lightModeIconBrush = requestUri.GetImageSourceAsync().Result;
                        }

                        return lightModeIconBrush ?? ImageExtensions.APIIcon.CurrentSource;
                    }));
                }),
                string.IsNullOrEmpty(DarkModeIconString)
                    ? null
                    : (() => { return Task.Run((() => ImageExtensions.LoadSvgFromBase64(DarkModeIconString))); }));
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

    [JsonIgnore] public bool SupportSystemPrompt => true;

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

    [JsonIgnore]
    public bool SupportStreaming
    {
        get
        {
            if (Capabilities?.TryGetValue("streaming", out var streaming) == true)
            {
                if (streaming is true)
                {
                    return true;
                }

                if (streaming is JsonElement { ValueKind: JsonValueKind.True })
                {
                    return true;
                }
            }

            return false;
        }
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
        get
        {
            if (Capabilities?.TryGetValue("structuredOutput", out var streaming) == true)
            {
                if (streaming is true)
                {
                    return true;
                }

                if (streaming is JsonElement { ValueKind: JsonValueKind.True })
                {
                    return true;
                }
            }

            return false;
        }
    }

/*Tags?.Contains("agents") == true;*/
    public bool FunctionCallOnStreaming { get; } = false;

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

    private FlowDocument? _document;

    [JsonIgnore]
    public FlowDocument? Document
    {
        get
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(DescriptionRaw);
            stringBuilder.AppendLine(NotesRaw);
            stringBuilder.AppendLine(EvaluationRaw);
            if (_document == null)
            {
                _document = stringBuilder.ToString().RenderOnFlowDocument();
            }

            return _document;
        }
    }

    [JsonPropertyName("publisher")] public string? Publisher { get; set; }

    [JsonPropertyName("logo_url")] public string? LogoUrl { get; set; }

    [JsonPropertyName("notes")] public string? NotesRaw { get; set; }

    [JsonPropertyName("evaluation")] public string? EvaluationRaw { get; set; }
    
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

    [JsonPropertyName("publisherSlug")] public string? PublisherSlug { get; set; }

    public const string FilteredInputText = "text";

    [JsonPropertyName("supported_input_modalities")]
    public string[]? SupportedInputModalities { get; set; }

    [JsonPropertyName("supported_output_modalities")]
    public string[]? SupportedOutputModalities { get; set; }

    [JsonPropertyName("capabilities")] public Dictionary<string, object?>? Capabilities { get; set; }

    [JsonPropertyName("isRestricted")] public bool? IsRestricted { get; set; }
}

public class AzureDetailModelInfo
{
    [JsonPropertyName("model")] public AzureModelInfo? ModelInfo { get; set; }

    [JsonPropertyName("modelEvaluation")] public ModelEvaluation? Evaluation { get; set; }

    [JsonPropertyName("modelInputSchema")] public ModelInputSchema? InputSchema { get; set; }

    [JsonPropertyName("modelReadme")] public string? Readme { get; set; }

    [JsonPropertyName("modelTransparencyContent")]
    public string? TransparencyContent { get; set; }

    [JsonPropertyName("playgroundUrl")] public string? PlaygroundUrl { get; set; }

    public class ModelEvaluation
    {
        [JsonPropertyName("examples")] public List<Example>? Examples { get; init; }

        [JsonPropertyName("sampleInputs")] public List<SampleInput>? SampleInputs { get; init; }

        [JsonPropertyName("inputs")] public List<ParameterDefinition>? Inputs { get; init; }

        [JsonPropertyName("outputs")] public List<ParameterDefinition>? Outputs { get; init; }

        [JsonPropertyName("fixedParameters")] public List<ParameterDefinition>? FixedParameters { get; init; }

        [JsonPropertyName("capabilities")] public Dictionary<string, object?>? Capabilities { get; init; }

        [JsonPropertyName("type")] public string? Type { get; init; }

        [JsonPropertyName("version")] public string? Version { get; init; }

        [JsonPropertyName("behavior")] public string? Behavior { get; init; }

        [JsonPropertyName("parameters")] public List<Parameter>? Parameters { get; init; }
    }

    public class Example
    {
        [JsonPropertyName("chatHistory")] public List<Message>? ChatHistory { get; init; }
    }

    public class SampleInput
    {
        [JsonPropertyName("messages")] public List<Message>? Messages { get; init; }
    }

    public class Message
    {
        [JsonPropertyName("role")] public string? Role { get; init; }

        [JsonPropertyName("content")] public string? Content { get; init; }
    }

    /// <summary>
    /// 通用参数定义，兼容 inputs、outputs、fixedParameters
    /// </summary>
    public class ParameterDefinition
    {
        [JsonPropertyName("key")] public string? Key { get; init; }

        [JsonPropertyName("friendlyName")] public string? FriendlyName { get; init; }

        [JsonPropertyName("type")] public string? Type { get; init; }

        [JsonPropertyName("payloadPath")] public string? PayloadPath { get; init; }

        [JsonPropertyName("required")] public bool? Required { get; init; }

        [JsonPropertyName("leftRole")] public string? LeftRole { get; init; }

        [JsonPropertyName("default")] public object? Default { get; init; }
    }

    public class Parameter
    {
        [JsonPropertyName("key")] public string? Key { get; init; }

        [JsonPropertyName("friendlyName")] public string? FriendlyName { get; init; }

        [JsonPropertyName("description")] public string? Description { get; init; }

        [JsonPropertyName("type")] public string? Type { get; init; }

        [JsonPropertyName("payloadPath")] public string? PayloadPath { get; init; }

        [JsonPropertyName("default")] public object? Default { get; init; }

        [JsonPropertyName("min")] public object? Min { get; init; }

        [JsonPropertyName("max")] public object? Max { get; init; }

        [JsonPropertyName("required")] public bool? Required { get; init; }
    }

    public class ModelInputSchema
    {
        [JsonPropertyName("examples")] public List<Example>? Examples { get; init; }

        [JsonPropertyName("sampleInputs")] public List<SampleInput>? SampleInputs { get; init; }

        [JsonPropertyName("inputs")] public List<ParameterDefinition>? Inputs { get; init; }

        [JsonPropertyName("outputs")] public List<ParameterDefinition>? Outputs { get; init; }

        [JsonPropertyName("fixedParameters")] public List<ParameterDefinition>? FixedParameters { get; init; }

        [JsonPropertyName("capabilities")] public Dictionary<string, object?>? Capabilities { get; init; }

        [JsonPropertyName("type")] public string? Type { get; init; }

        [JsonPropertyName("version")] public string? Version { get; init; }

        [JsonPropertyName("behavior")] public string? Behavior { get; init; }

        [JsonPropertyName("parameters")] public List<Parameter>? Parameters { get; init; }
    }
}