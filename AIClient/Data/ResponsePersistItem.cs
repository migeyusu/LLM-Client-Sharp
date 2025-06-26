using System.Text.Json.Serialization;
using LLMClient.UI;
using LLMClient.UI.Dialog;

namespace LLMClient.Data;

public class ResponsePersistItem: IResponse
{
    public string ModelName { get; set; } = string.Empty;

    public string EndPointName { get; set; } = string.Empty;

    public bool IsInterrupt { get; set; }

    public long Tokens { get; set; }

    public string? ErrorMessage { get; set; }
    
    public double? Price { get; set; }

    public string? Raw { get; set; }

    public int Latency { get; set; }

    public int Duration { get; set; }

    [JsonPropertyName("IsEnable")] public bool IsAvailableInContext { get; set; }
}