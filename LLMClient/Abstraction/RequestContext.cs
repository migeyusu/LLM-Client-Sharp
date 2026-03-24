using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public sealed class RequestContext
{
    public required List<ChatMessage> ChatHistory { get; init; }

    public required FunctionCallEngine FunctionCallEngine { get; init; }

    public required ChatOptions RequestOptions { get; init; }

    public AdditionalPropertiesDictionary? TempAdditionalProperties { get; init; }

    public bool ShowRequestJson { get; set; }
}