using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Endpoints.Converters;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI.Dialog;

namespace LLMClient.Endpoints;

[JsonDerivedType(typeof(OpenRouterReasoningConfig), "openrouter")]
[JsonDerivedType(typeof(GeekAIThinkingConfig), "geekai")]
public interface IThinkingConfig
{
    public string? Effort { get; }

    public int? BudgetTokens { get; }

    public bool ShowThinking { get; }

    public static IThinkingConfig? Get(ILLMModel model)
    {
        if (model.Endpoint is APIEndPoint apiEndPoint)
        {
            switch (apiEndPoint.ModelsSource)
            {
                case ModelSource.OpenRouter:
                    return new OpenRouterReasoningConfig();
                case ModelSource.GeekAI:
                    return new GeekAIThinkingConfig();
                default:
                    return null;
            }
        }

        return null;
    }

    public void EnableThinking(RequestViewItem requestViewItem)
    {
        if (this is OpenRouterReasoningConfig)
        {
            requestViewItem.AdditionalProperties["reasoning"] = this;
        }
        else if (this is GeekAIThinkingConfig)
        {
            requestViewItem.AdditionalProperties["thinking_config"] = this;
        }
        else
        {
            throw new NotSupportedException();
        }
    }
}