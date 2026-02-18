using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using MaterialDesignThemes.Wpf;

namespace LLMClient.Endpoints;

/// <summary>
/// 测试输出固定文本
/// </summary>
public class StubLLMChatModel : IEndpointModel
{
    public static StubLLMChatModel Instance { get; } = new StubLLMChatModel();

    public string? SeriesName { get; } = string.Empty;

    public string? Provider { get; } = string.Empty;
    public string APIId { get; set; } = Guid.NewGuid().ToString();

    public string Name
    {
        get => "TestLLMChatModel";
        set => throw new NotImplementedException();
    }

    public ThemedIcon Icon { get; }

    public int MaxContextSize { get; set; } = int.MaxValue;

    public string DisplayName
    {
        get => "Test Chat Model";
        set => throw new NotImplementedException();
    }

    public ILLMAPIEndpoint Endpoint
    {
        get { return StubEndPoint.Instance; }
    }

    public ThinkingIncludeMode ThinkingIncludeMode { get; } = ThinkingIncludeMode.None;

    public bool SupportSystemPrompt { get; set; } = true;
    public bool TopPEnable { get; set; } = true;
    public bool TopKEnable { get; set; } = true;
    public bool TemperatureEnable { get; set; } = true;
    public bool MaxTokensEnable { get; set; } = true;
    public bool FrequencyPenaltyEnable { get; set; } = true;
    public bool PresencePenaltyEnable { get; set; } = true;
    public bool SeedEnable { get; } = true;

    public int TopKMax { get; set; } = 1;
    public int MaxTokenLimit { get; } = int.MaxValue;
    public bool Reasonable { get; } = true;
    public bool SupportStreaming { get; } = true;

    public bool SupportAudioInput { get; } = true;

    public bool SupportVideoInput { get; } = true;
    public bool SupportImageInput { get; } = true;

    public bool SupportTextGeneration { get; } = true;

    public bool SupportImageGeneration { get; } = false;

    public bool SupportAudioGeneration { get; } = false;

    public bool SupportVideoGeneration { get; } = false;

    public bool SupportSearch { get; set; } = true;
    public bool SupportFunctionCall { get; set; } = true;
    public bool FunctionCallOnStreaming { get; set; } = true;
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

    public StubLLMChatModel()
    {
        Icon = LocalThemedIcon.FromPackIcon(PackIconKind.TestTube);
    }
}

public class StubEndPoint : ILLMAPIEndpoint
{
    public static StubEndPoint Instance { get; } = new("Stub Endpoint");

    public StubEndPoint(string name)
    {
        Name = name;
        Icon = LocalThemedIcon.FromPackIcon(PackIconKind.TestTube);
    }

    public string DisplayName
    {
        get { return Name; }
    }

    public bool IsInbuilt { get; } = true;
    public bool IsEnabled { get; } = true;
    public string Name { get; }
    public ThemedIcon Icon { get; }

    public IReadOnlyCollection<IEndpointModel> AvailableModels => [StubLLMChatModel.Instance];

    public ILLMChatClient? NewChatClient(IEndpointModel model)
    {
        return new StubLlmClient();
    }

    public IEndpointModel? GetModel(string modelName)
    {
        return new StubLLMChatModel();
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
}

public class EmptyLLMChatModel : IEndpointModel
{
    public static EmptyLLMChatModel Instance { get; } = new EmptyLLMChatModel();

    public string? SeriesName { get; } = string.Empty;

    public string? Provider { get; } = string.Empty;
    public string APIId { get; set; } = Guid.NewGuid().ToString();

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