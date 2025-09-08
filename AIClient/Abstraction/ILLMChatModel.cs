using LLMClient.UI.Component;

namespace LLMClient.Abstraction;

public interface ILLMChatModel : IModelParams
{
    string Id { get; }

    /// <summary>
    /// friendly name
    /// </summary>
    string Name { get; }

    ThemedIcon Icon { get; }

    int MaxContextSize { get; }

    ILLMEndpoint Endpoint { get; }

    #region switch

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
}