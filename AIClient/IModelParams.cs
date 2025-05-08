using System.Text.Json.Serialization;
using LLMClient.Endpoints.OpenAIAPI;

namespace LLMClient;

[JsonDerivedType(typeof(DefaultModelParam), "default")]
public interface IModelParams
{
    bool Streaming { get; set; }
    
    string? SystemPrompt { get; set; }

    float TopP { get; set; }

    int TopK { get; set; }

    float Temperature { get; set; }

    int MaxTokens { get; set; }

    float FrequencyPenalty { get; set; }

    float PresencePenalty { get; set; }

    long? Seed { get; set; }
}