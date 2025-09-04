using System.Text.Json.Serialization;

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

public class GeekAIThinkingConfig : IThinkingConfig
{
    [JsonPropertyName("reasoning_effort")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Effort { get; set; } = "low";

    [JsonPropertyName("budget_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BudgetTokens { get; set; } = 1000;

    [JsonPropertyName("include_thoughts")] public bool ShowThinking { get; set; } = true;

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