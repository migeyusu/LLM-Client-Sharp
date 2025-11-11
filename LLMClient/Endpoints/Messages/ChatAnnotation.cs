using System.Text.Json.Serialization;

namespace LLMClient.Endpoints.Messages;

public class ChatAnnotation
{
    [JsonPropertyName("type")] public string? Type { get; set; }

    [JsonPropertyName("url_citation")] public UrlCitation? UrlCitation { get; set; }
}