using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace LLMClient.Data;

public class ChatMessagePO
{
    [JsonPropertyName("authorName")] public string? AuthorName { get; set; }

    [JsonPropertyName("role")] public ChatRole Role { get; set; } = ChatRole.Assistant;

    [JsonPropertyName("messageId")] public string? MessageId { get; set; }


    [JsonPropertyName("additionalProperties")]
    public AdditionalPropertiesDictionary? AdditionalProperties { get; set; }

    public IList<IAIContent> Contents { get; set; } = [];
}