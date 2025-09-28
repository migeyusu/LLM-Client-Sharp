﻿using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Dialog;
using LLMClient.Endpoints.Converters;
using LLMClient.Endpoints.OpenAIAPI;

namespace LLMClient.Endpoints;

[JsonDerivedType(typeof(OpenRouterReasoningConfig), "openrouter")]
[JsonDerivedType(typeof(GeekAIThinkingConfig), "geekai")]
[JsonDerivedType(typeof(NVDAAPIThinkingConfig), "nvda")]
public interface IThinkingConfig : ICloneable
{
    public string? Effort { get; set; }

    public int? BudgetTokens { get; set; }

    public bool ShowThinking { get; set; }

    public static IThinkingConfig? Get(ILLMChatModel model)
    {
        if (model.Endpoint is APIEndPoint apiEndPoint)
        {
            switch (apiEndPoint.Option.ModelsSource)
            {
                case ModelSource.OpenRouter:
                    return new OpenRouterReasoningConfig();
                case ModelSource.GeekAI:
                    return new GeekAIThinkingConfig();
                default:
                    break;
            }

            if (apiEndPoint.Option.ConfigOption.URL == "https://integrate.api.nvidia.com/v1")
            {
                return new NVDAAPIThinkingConfig();
            }
        }

        return null;
    }


    public void EnableThinking(RequestViewItem requestViewItem);
}