using System.Text.Json.Serialization;

namespace LLMClient.Endpoints.Messages;

/// <summary>
/// 用于处理标准Openai API协议未定义的扩展内容
/// </summary>
public class Extended
{
    public sealed class ChatChunk
    {
        [JsonPropertyName("choices")] public List<Choice>? Choices { get; set; }
    }

    public sealed class Choice
    {
        [JsonPropertyName("index")] public int Index { get; set; }
        [JsonPropertyName("delta")] public Delta? Delta { get; set; }
    }


    public sealed class Delta
    {
        [JsonPropertyName("reasoning_content")]
        public string? ReasoningContent { get; set; }
    }
}