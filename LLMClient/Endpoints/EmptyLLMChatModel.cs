using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;

namespace LLMClient.Endpoints;

public class EmptyLLMChatModel : ILLMModel
{
    public static EmptyLLMChatModel Instance { get; } = new EmptyLLMChatModel();

    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name
    {
        get => "EmptyModel";
        set => throw new NotImplementedException();
    }

    public ThemedIcon Icon { get; set; } = ThemedIcon.EmptyIcon;

    public int MaxContextSize { get; set; }

    public string DisplayName
    {
        get => "Empty Model";
        set => throw new NotImplementedException();
    }

    public ILLMAPIEndpoint Endpoint
    {
        get => EmptyLLMEndpoint.Instance;
        set => throw new NotImplementedException();
    }

    public ThinkingIncludeMode ThinkingIncludeMode { get; } = ThinkingIncludeMode.None;

    public bool SupportSystemPrompt { get; set; }
    public bool TopPEnable { get; set; }
    public bool TopKEnable { get; set; }
    public bool TemperatureEnable { get; set; }
    public bool MaxTokensEnable { get; set; }
    public bool FrequencyPenaltyEnable { get; set; }
    public bool PresencePenaltyEnable { get; set; }
    public bool SeedEnable { get; set; }
    public int TopKMax { get; set; }
    public int MaxTokenLimit { get; set; }
    public bool Reasonable { get; set; }
    public bool SupportStreaming { get; set; }
    public bool SupportAudioInput { get; set; }
    public bool SupportVideoInput { get; set; }
    public bool SupportImageInput { get; set; }
    public bool SupportTextGeneration { get; set; }
    public bool SupportImageGeneration { get; set; }
    public bool SupportAudioGeneration { get; set; }
    public bool SupportVideoGeneration { get; set; }
    public bool SupportSearch { get; set; }
    public bool SupportFunctionCall { get; set; }
    public bool FunctionCallOnStreaming { get; set; }
    public IPriceCalculator? PriceCalculator { get; init; }

    public UsageCount? Telemetry { get; set; }
    public bool Streaming { get; set; }
    public string? SystemPrompt { get; set; }
    public float TopP { get; set; }
    public int TopK { get; set; }
    public float Temperature { get; set; }
    public int MaxTokens { get; set; }
    public float FrequencyPenalty { get; set; }
    public float PresencePenalty { get; set; }
    public long? Seed { get; set; }

    public IThinkingConfig? ThinkingConfig { get; set; }
    public bool ThinkingEnabled { get; set; }
}