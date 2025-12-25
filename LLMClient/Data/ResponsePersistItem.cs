using System.Text.Json.Serialization;
using LLMClient.Endpoints.Messages;
using Microsoft.Extensions.AI;
using ChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;

namespace LLMClient.Data;

public class ResponsePersistItem
{
    public ParameterizedLLMModelPO? Client { get; set; }

    public bool IsInterrupt { get; set; }

    public long Tokens { get; set; }

    public UsageDetails? Usage { get; set; }

    public int Latency { get; set; }

    public int Duration { get; set; }

    public string? ErrorMessage { get; set; }

    public double? Price { get; set; }

    public IList<ChatMessagePO>? ResponseMessages { get; set; }

    public IList<ChatAnnotation>? Annotations { get; set; }

    public ChatFinishReason? FinishReason { get; set; }

    public bool IsManualValid { get; set; } = false;

    public bool IsAvailableInContextSwitch { get; set; } = true;

    [JsonPropertyName("IsEnable")] public bool IsAvailableInContext { get; set; }
}