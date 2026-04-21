using LLMClient.Endpoints;
using LLMClient.ToolCall;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public interface IChatRequest
{
    string? UserPrompt { get; }

    ISearchOption? SearchOption { get; }

    AIContextProvider[]? ContextProviders { get; }

    List<CheckableFunctionGroupTree>? FunctionGroups { get; }

    IRagSource[]? RagSources { get; }

    ChatResponseFormat? ResponseFormat { get; }

    FunctionCallEngineType CallEngineType { get; }

    AdditionalPropertiesDictionary? TempAdditionalProperties { get; }

    bool IsDebugMode { get; }

    bool AutoApproveAllInvocations { get; }
}