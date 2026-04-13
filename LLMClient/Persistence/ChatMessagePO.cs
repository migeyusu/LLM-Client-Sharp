using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace LLMClient.Persistence;

public class ChatMessagePO
{
    [JsonPropertyName("authorName")] public string? AuthorName { get; set; }

    [JsonPropertyName("role")] public ChatRole Role { get; set; } = ChatRole.Assistant;

    [JsonPropertyName("messageId")] public string? MessageId { get; set; }

    public IList<IAIContent> Contents { get; set; } = [];

    /// <summary>
    /// 仅持久化可安全序列化的 long 型附加属性（如 TokensCounter），
    /// 不直接暴露 AdditionalPropertiesDictionary 以防止不可序列化对象污染。
    /// </summary>
    [JsonPropertyName("additionalProperties")]
    public Dictionary<string, long>? AdditionalProperties { get; set; }
}