using LLMClient.Component.CustomControl;

namespace LLMClient.Abstraction;

/// <summary>
/// 不同的服务商提供相同模型的不同特性
/// </summary>
public interface IEndpointModel : IModel, IModelParams
{
    /// <summary>
    /// 用于api的id
    /// </summary>
    string APIId { get; }

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