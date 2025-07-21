using System.Text.Json;
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
    [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
}

public class DataContentPO : IAIContent
{
    [JsonPropertyName("mediatype")] public string MediaType { get; set; } = string.Empty;

    [JsonPropertyName("uri")] public string? Uri { get; set; }

    [JsonPropertyName("data")] public byte[]? Data { get; set; }
}

public class ErrorContentPO : IAIContent
{
    [JsonPropertyName("details")] public string? Details { get; set; }

    [JsonPropertyName("message")] public string? Message { get; set; }

    [JsonPropertyName("errorcode")] public string? ErrorCode { get; set; }
}

public class FunctionResultContentPO : IAIContent
{
    [JsonPropertyName("callid")] public string CallId { get; set; } = string.Empty;

    [JsonPropertyName("exception")] public Exception? Exception { get; set; }

    [JsonPropertyName("result")] public object? Result { get; set; }
}

public class TextReasoningContentPO : IAIContent
{
    [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
}

public class UriContentPO : IAIContent
{
    [JsonPropertyName("uri")] public Uri? Uri { get; set; }

    [JsonPropertyName("mediatype")] public string? MediaType { get; set; }
}

public class UsageContentPO : IAIContent
{
    [JsonPropertyName("usagedetails")] public UsageDetails? UsageDetails { get; set; }
}