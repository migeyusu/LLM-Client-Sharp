using LLMClient.Endpoints;
using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public interface IChatRequest
{
    string? UserPrompt { get; }
    
    ISearchOption? SearchOption { get; }

    List<IAIFunctionGroup>? FunctionGroups { get; }

    IRagSource[]? RagSources { get; }

    ChatResponseFormat? ResponseFormat { get; }

    FunctionCallEngineType CallEngineType { get; }

    AdditionalPropertiesDictionary? TempAdditionalProperties { get; }

    bool IsDebugMode { get; }

    bool AutoApproveAllInvocations { get; }
}