using System.Text.Json.Serialization;

namespace LLMClient.Data;

public class ResponsePersistItem
{
    public string ModelName { get; set; } = string.Empty;

    public string EndPointName { get; set; } = string.Empty;

    public bool IsInterrupt { get; set; }

    public long Tokens { get; set; }

    public string? ErrorMessage { get; set; }

    public string? Raw { get; set; }

    [JsonPropertyName("IsEnable")]
    public bool IsAvailableInContext { get; set; }
}