using System.Text.Json.Serialization;

namespace LLMClient.Endpoints.Messages;

public class ChatAnnotation
{
    [JsonPropertyName("type")] public string? Type { get; set; }

    [JsonPropertyName("url_citation")] public UrlCitation? UrlCitation { get; set; }
}

public class UrlCitation
{
    [JsonPropertyName("end_index")] public int EndIndex { get; set; }

    [JsonPropertyName("start_index")] public int StartIndex { get; set; }

    [JsonPropertyName("title")] public string? Title { get; set; }

    [JsonPropertyName("url")] public string? Url { get; set; }

    [JsonPropertyName("content")] public string? Content { get; set; }
}