using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using OpenAI.Responses;

namespace LLMClient.Endpoints;

public class OpenRouterReasoningConfig : IThinkingConfig
{
    [JsonPropertyName("effort")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Effort { get; set; }

    [JsonPropertyName("max_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BudgetTokens { get; set; } = 1000;

    [JsonPropertyName("exclude")] public bool Exclude { get; set; }

    [JsonIgnore]
    public bool ShowThinking
    {
        get => !Exclude;
        set => Exclude = !value;
    }

    public void ApplyThinking(ChatOptions options)
    {
        object clone = this.Clone();
        options.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        options.AdditionalProperties["reasoning"] = clone;
    }

    public object Clone()
    {
        return new OpenRouterReasoningConfig()
        {
            Effort = this.Effort,
            BudgetTokens = this.BudgetTokens,
            Exclude = this.Exclude
        };
    }
}

public class DefaultThinkingConfig : IThinkingConfig
{
    [JsonIgnore] public string? Effort { get; set; }

    [JsonIgnore] public int? BudgetTokens { get; set; }

    [JsonIgnore] public bool ShowThinking { get; set; }

    public void ApplyThinking(ChatOptions options)
    {
        options.RawRepresentationFactory = client => new CreateResponseOptions
        {
            ReasoningOptions = new()
            {
                ReasoningEffortLevel = string.IsNullOrEmpty(Effort)
                    ? ResponseReasoningEffortLevel.High
                    : new ResponseReasoningEffortLevel(Effort),
                ReasoningSummaryVerbosity = ShowThinking
                    ? ResponseReasoningSummaryVerbosity.Detailed
                    : ResponseReasoningSummaryVerbosity.Auto
            }
        };
    }

    public object Clone()
    {
        return new DefaultThinkingConfig();
    }
}

public class GeekAIThinkingConfig : IThinkingConfig
{
    [JsonPropertyName("reasoning_effort")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Effort { get; set; } = "low";

    [JsonPropertyName("budget_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BudgetTokens { get; set; } = 1000;

    [JsonPropertyName("include_thoughts")] public bool ShowThinking { get; set; } = true;

    public void ApplyThinking(ChatOptions options)
    {
        object clone = this.Clone();
        options.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        options.AdditionalProperties["thinking_config"] = clone;
    }

    //仅qwen3早期支持
    // _requestViewItem.AdditionalProperties["enable_thinking"] = true;
    public object Clone()
    {
        return new GeekAIThinkingConfig()
        {
            Effort = this.Effort,
            BudgetTokens = this.BudgetTokens,
            ShowThinking = this.ShowThinking
        };
    }
}

/// <summary>
/// nvidia的api，当前只有deepseek v3.1支持
/// </summary>
public class NVDAAPIThinkingConfig : IThinkingConfig
{
    public object Clone()
    {
        return new NVDAAPIThinkingConfig();
    }

    [JsonIgnore] public string? Effort { get; set; }

    [JsonIgnore] public int? BudgetTokens { get; set; }

    [JsonIgnore] public bool ShowThinking { get; set; }

    public void ApplyThinking(ChatOptions options)
    {
        options.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        options.AdditionalProperties["chat_template_kwargs"] = new Dictionary<string, object>
        {
            { "thinking", true }
        };
    }
}