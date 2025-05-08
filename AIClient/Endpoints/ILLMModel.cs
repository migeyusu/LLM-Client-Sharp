using System.Windows.Media;

namespace LLMClient.Endpoints;

public interface ILLMModel : IModelParams
{
    string Id { get; }

    /// <summary>
    /// friendly name
    /// </summary>
    string Name { get; }

    bool Streaming { get; }

    ImageSource? Icon { get; }

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

    #endregion
}