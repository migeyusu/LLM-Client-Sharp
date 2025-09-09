﻿using System.Collections.ObjectModel;
using LLMClient.Abstraction;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.MCP;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace LLMClient.Endpoints;

public class NullLlmModelClient : ILLMChatClient
{
    public static NullLlmModelClient Instance => new NullLlmModelClient();

    public string Name { get; } = "NullLlmModelClient";

    public ILLMEndpoint Endpoint
    {
        get
        {
            return new APIEndPoint(new APIEndPointOption() { Name = "NullLlmModelClient" }, NullLoggerFactory.Instance);
        }
    }

    public ILLMChatModel Model
    {
        get
        {
            return new APIModelInfo
            {
                Id = "fake-model",
                Name = "Fake Model",
                IsNotMatchFromSource = false,
                Streaming = true,
                UrlIconEnable = false,
                IconType = ModelIconType.None,
                IconUrl = null,
                InfoUrl = "https://example.com/fake-model",
                Description = "This is a fake model for testing purposes.",
                MaxContextSize = 0,
                Endpoint = new NullLLMEndpoint(),
                SupportSystemPrompt = true,
                TopPEnable = true,
                TopKEnable = true,
                TemperatureEnable = true,
                MaxTokensEnable = true,
                FrequencyPenaltyEnable = true,
                PresencePenaltyEnable = true,
                SeedEnable = true,
                SystemPrompt = "默认系统提示",
                TopP = 0.9f,
                TopKMax = 100,
                TopK = 40,
                Temperature = 0.7f,
                MaxTokens = 2048,
                MaxTokenLimit = 4096,
                Reasonable = true,
                FunctionCallOnStreaming = true,
                SupportStreaming = true,
                SupportImageGeneration = true,
                SupportAudioGeneration = true,
                SupportVideoGeneration = true,
                SupportSearch = true,
                SupportFunctionCall = true,
                SupportAudioInput = true,
                SupportVideoInput = true,
                SupportTextGeneration = true,
                SupportImageInput = true,
                PriceCalculator = new TokenBasedPriceCalculator(),
                FrequencyPenalty = 0.5f,
                PresencePenalty = 0.5f,
                Seed = 42
            };
        }
    }

    public bool IsResponding { get; set; } = false;

    public IModelParams Parameters { get; set; } = new DefaultModelParam
    {
        Streaming = false,
        SystemPrompt = null,
        TopP = 0.9f,
        TopK = 40,
        Temperature = 0.7f,
        MaxTokens = 4096,
        FrequencyPenalty = 0.5f,
        PresencePenalty = 0.5f,
        Seed = 666
    };

    public IFunctionInterceptor FunctionInterceptor { get; set; } = FunctionAuthorizationInterceptor.Instance;
    public ObservableCollection<string> RespondingText { get; } = new();

    public Task<CompletedResult> SendRequest(DialogContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CompletedResult
        {
            Usage = new UsageDetails
            {
                InputTokenCount = 0,
                OutputTokenCount = 0,
                TotalTokenCount = 0,
                AdditionalCounts = null
            },
            Latency = 0,
            Duration = 0,
            ErrorMessage = null,
            Price = null,
            FinishReason = ChatFinishReason.Stop,
            ResponseMessages =
            [
                new ChatMessage(ChatRole.Assistant, "This is a fake response from NullLlmModelClient.")
            ],
            Annotations = null,
            AdditionalProperties = null
        });
    }
}