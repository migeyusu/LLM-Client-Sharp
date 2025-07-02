using System.Text.Json.Serialization;
using LLMClient.UI;
using LLMClient.UI.Dialog;
using Microsoft.Extensions.AI;

namespace LLMClient.Data;

public class ResponsePersistItem : IResponse
{
    public string ModelName { get; set; } = string.Empty;

    public string EndPointName { get; set; } = string.Empty;

    public bool IsInterrupt { get; set; }

    public long Tokens { get; set; }

    public string? ErrorMessage { get; set; }

    public double? Price { get; set; }
    public IList<ChatMessage>? ResponseMessages { get; set; }

    public ChatFinishReason? FinishReason { get; set; }
    
    public int Latency { get; set; }

    public int Duration { get; set; }

    [JsonPropertyName("IsEnable")] public bool IsAvailableInContext { get; set; }
}