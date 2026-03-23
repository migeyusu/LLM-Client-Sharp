using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public sealed class PreparedRequestContext
{
    public required List<ChatMessage> ChatHistory { get; init; }

    public required FunctionCallEngine FunctionCallEngine { get; init; }

    public required ChatOptions RequestOptions { get; init; }

    public AdditionalPropertiesDictionary? TempAdditionalProperties { get; init; }
}