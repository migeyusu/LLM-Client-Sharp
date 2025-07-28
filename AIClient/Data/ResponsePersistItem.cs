using System.Text.Json.Serialization;
using LLMClient.Endpoints.Messages;
using ChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;

namespace LLMClient.Data;

public class ResponsePersistItem
{
    public LLMClientPersistModel? Client { get; set; }

    public bool IsInterrupt { get; set; }

    public long Tokens { get; set; }

    public string? ErrorMessage { get; set; }

    public double? Price { get; set; }

    public IList<ChatMessagePO>? ResponseMessages { get; set; }

    public IList<ChatAnnotation>? Annotations { get; set; }

    public ChatFinishReason? FinishReason { get; set; }

    public int Latency { get; set; }

    public int Duration { get; set; }

    [JsonPropertyName("IsEnable")] public bool IsAvailableInContext { get; set; }
}
