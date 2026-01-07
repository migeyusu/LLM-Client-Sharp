using System.ComponentModel;
using System.Text.Json.Serialization;
using LLMClient.Component.Converters;
using LLMClient.Component.CustomControl;
using LLMClient.Rag;

namespace LLMClient.Abstraction;

[TypeConverter(typeof(EnumDescriptionTypeConverter))]
[JsonConverter(typeof(JsonStringEnumConverter<SearchAlgorithm>))]
public enum ThinkingIncludeMode
{
    /// <summary>
    /// include nothing between multi-turn dialogues
    /// </summary>
    [Description("不包含思考内容")]
    None = 0,

    /// <summary>
    /// always include all thinking content between multi-turn dialogues
    /// </summary>
    [Description("包含所有思考内容")]
    All = 1,

    /// <summary>
    /// only keep the last dialogue's thinking content between multi-turn dialogues
    /// </summary>
    [Description("仅保留最后一次思考内容")]
    KeepLast = 2,
}

public interface ILLMModel : IModelParams
{
    string Id { get; }

    /// <summary>
    /// friendly name
    /// </summary>
    string Name { get; }

    ThemedIcon Icon { get; }

    int MaxContextSize { get; }

    ILLMAPIEndpoint Endpoint { get; }

    ThinkingIncludeMode ThinkingIncludeMode { get; }

    #region feature

    bool SupportSystemPrompt { get; }

    bool TopPEnable { get; }
    bool TopKEnable { get; }
    bool TemperatureEnable { get; }
    bool MaxTokensEnable { get; }
    bool FrequencyPenaltyEnable { get; }
    bool PresencePenaltyEnable { get; }
    bool SeedEnable { get; }
    int TopKMax { get; }
    int MaxTokenLimit { get; }

    /// <summary>
    /// 推理模型，是表示推理模型，但不代表需要手动开启推理
    /// </summary>
    bool Reasonable { get; }

    bool SupportStreaming { get; }

    bool SupportAudioInput { get; }

    bool SupportVideoInput { get; }

    bool SupportImageInput { get; }

    bool SupportTextGeneration { get; }

    bool SupportImageGeneration { get; }

    bool SupportAudioGeneration { get; }

    bool SupportVideoGeneration { get; }

    bool SupportSearch { get; }

    bool SupportFunctionCall { get; }

    bool FunctionCallOnStreaming { get; }

    #endregion

    IPriceCalculator? PriceCalculator { get; }

    UsageCount? Telemetry { get; set; }
}