using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

// 这个类是 FunctionCallContent 的一个简单数据表示
// 它可以被源码生成器轻松处理
namespace LLMClient.Data;

[JsonDerivedType(typeof(FunctionCallContentPO), "function_call")]
[JsonDerivedType(typeof(TextContentPO), "text")]
[JsonDerivedType(typeof(DataContentPO), "data")]
[JsonDerivedType(typeof(ErrorContentPO), "error")]
[JsonDerivedType(typeof(FunctionResultContentPO), "function_result")]
[JsonDerivedType(typeof(TextReasoningContentPO), "text_reasoning")]
[JsonDerivedType(typeof(UriContentPO), "uri")]
[JsonDerivedType(typeof(UsageContentPO), "usage")]
public interface IAIContent
{
}

public class FunctionCallContentPO : IAIContent
{
    [JsonPropertyName("callid")] public string CallId { get; set; } = string.Empty;

    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")] public IDictionary<string, object?>? Arguments { get; set; }
}

public class TextContentPO : IAIContent
{
    [JsonPropertyName("Text")] public string Text { get; set; } = string.Empty;
}

public class DataContentPO : IAIContent
{
    [JsonPropertyName("MediaType")] public string MediaType { get; set; } = string.Empty;

    [JsonPropertyName("Uri")] public string? Uri { get; set; }

    [JsonPropertyName("Data")] public byte[]? Data { get; set; }
}

public class ErrorContentPO : IAIContent
{
    [JsonPropertyName("Details")] public string? Details { get; set; }

    [JsonPropertyName("Message")] public string? Message { get; set; }

    [JsonPropertyName("ErrorCode")] public string? ErrorCode { get; set; }
}

public class FunctionResultContentPO : IAIContent
{
    [JsonPropertyName("CallId")] public string CallId { get; set; } = string.Empty;

    [JsonPropertyName("Exception")] public Exception? Exception { get; set; }

    [JsonPropertyName("Result")] public object? Result { get; set; }
}

public class TextReasoningContentPO : IAIContent
{
    [JsonPropertyName("Text")] public string Text { get; set; } = string.Empty;
}

public class UriContentPO : IAIContent
{
    [JsonPropertyName("Uri")] public Uri? Uri { get; set; }

    [JsonPropertyName("MediaType")] public string? MediaType { get; set; }
}

public class UsageContentPO : IAIContent
{
    [JsonPropertyName("UsageDetails")] public UsageDetails? UsageDetails { get; set; }
}