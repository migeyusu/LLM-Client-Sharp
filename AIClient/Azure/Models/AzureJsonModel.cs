namespace LLMClient.Azure.Models;

public class AzureJsonModel: IModelParams
{
    public int TotalTokens { get; set; }

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public string? SystemPrompt { get; set; }

    public float TopP { get; set; }

    public int TopK { get; set; }

    public float Temperature { get; set; }

    public int MaxTokens { get; set; }

    public float FrequencyPenalty { get; set; }

    public float PresencePenalty { get; set; }

    public long? Seed { get; set; }
}