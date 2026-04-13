using System.Text.Json.Serialization;
using LLMClient.Endpoints.Messages;
using Microsoft.Extensions.AI;
using ChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;

namespace LLMClient.Persistence;

public class ResponsePersistItemBase
{
    public int Latency { get; set; }

    public int Duration { get; set; }

    public string? ErrorMessage { get; set; }

    public double? Price { get; set; }

    public UsageDetails? Usage { get; set; }

    public UsageDetails? LastSuccessfulUsage { get; set; }

    public bool IsInterrupt { get; set; }
    
    [JsonPropertyName("ResponseMessages")]
    public IList<ChatMessagePO>? Messages { get; set; }

    public IList<ChatAnnotation>? Annotations { get; set; }

    public ChatFinishReason? FinishReason { get; set; }
}


public class RawResponsePersistItem : ResponsePersistItemBase
{
    
}

public class ClientResponsePersistItem : ResponsePersistItemBase
{
    public ParameterizedLLMModelPO? Client { get; set; }

    public bool IsManualValid { get; set; } = false;

    public bool IsAvailableInContextSwitch { get; set; } = true;

    [JsonPropertyName("IsEnable")] public bool IsAvailableInContext { get; set; }
}