using System.Windows.Media;
using LLMClient.UI.Component;

namespace LLMClient.Abstraction;

public interface ILLMModel : IModelParams
{
    string Id { get; }

    /// <summary>
    /// friendly name
    /// </summary>
    string Name { get; }

    ThemedIcon Icon { get; }

    int MaxContextSize { get; }

    ILLMEndpoint? Endpoint { get; }

    #region switch

    bool SystemPromptEnable { get; }
    bool TopPEnable { get; }
    bool TopKEnable { get; }
    bool TemperatureEnable { get; }
    bool MaxTokensEnable { get; }
    bool FrequencyPenaltyEnable { get; }
    bool PresencePenaltyEnable { get; }
    bool SeedEnable { get; }
    int TopKMax { get; }
    int MaxTokenLimit { get; }
    bool ReasoningEnable { get; }

    #endregion

    IPriceCalculator? PriceCalculator { get; }
}