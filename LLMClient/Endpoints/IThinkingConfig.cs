using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Dialog;
using LLMClient.Endpoints.Converters;
using LLMClient.Endpoints.OpenAIAPI;
using Microsoft.Extensions.AI;

namespace LLMClient.Endpoints;

[JsonDerivedType(typeof(OpenRouterReasoningConfig), "openrouter")]
[JsonDerivedType(typeof(GeekAIThinkingConfig), "geekai")]
[JsonDerivedType(typeof(NVDAAPIThinkingConfig), "nvda")]
[JsonDerivedType(typeof(DefaultThinkingConfig), "default")]
[JsonDerivedType(typeof(ThinkingConfigViewModel), "vm")]
public interface IThinkingConfig
{
    public string? Effort { get; set; }

    public int? BudgetTokens { get; set; }

    public bool ShowThinking { get; set; }

    public static IThinkingConfig? CreateFrom(ILLMAPIEndpoint endpoint, ThinkingConfigViewModel? configViewModel)
    {
        if (configViewModel == null)
        {
            return null;
        }

        var baseConfig = Create(endpoint);
        if (baseConfig == null)
        {
            return null;
        }

        baseConfig.Effort = configViewModel.Effort;
        if (configViewModel.EnableBudgetTokens)
        {
            baseConfig.BudgetTokens = configViewModel.BudgetTokens;
        }

        baseConfig.ShowThinking = configViewModel.ShowThinking;
        return baseConfig;
    }

    private static IThinkingConfig? Create(ILLMAPIEndpoint endpoint)
    {
        if (endpoint is APIEndPoint apiEndPoint)
        {
            switch (apiEndPoint.Option.ModelsSource)
            {
                case ModelSource.OpenRouter:
                    return new OpenRouterReasoningConfig();
                case ModelSource.GeekAI:
                    return new GeekAIThinkingConfig();
            }

            if (apiEndPoint.Option.ConfigOption.URL == "https://integrate.api.nvidia.com/v1")
            {
                return new NVDAAPIThinkingConfig();
            }

            if (apiEndPoint.Option.ConfigOption.IsOpenAICompatible)
            {
                return new DefaultThinkingConfig();
            }
        }

        return null;
    }


    public void ApplyThinking(ChatOptions options);
}