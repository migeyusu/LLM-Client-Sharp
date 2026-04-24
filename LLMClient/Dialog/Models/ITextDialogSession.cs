using LLMClient.Abstraction;
using LLMClient.ToolCall;
using Microsoft.Agents.AI;

namespace LLMClient.Dialog.Models;

public interface ITextDialogSession
{
    /// <summary>
    /// session id
    /// </summary>
    Guid ID { get; }

    IReadOnlyList<IDialogItem> VisualDialogItems { get; }

    IResponseItem WorkingResponse { get; }

    string? WorkingDirectory { get; }

    IEnumerable<IDialogItem> GetChatHistory()
    {
        return WorkingResponse.GetChatHistory();
    }

    Task CutContextAsync(IRequestItem? requestItem = null);

    AIContextProvider[]? ContextProviders { get; }

    IPromptCommandAggregate? PromptCommand { get; }

    string? SystemPrompt { get; }

    IEnumerable<Type> SupportedAgents { get; }

    IFunctionGroupSource? ToolsSource { get; }

    Task<IResponse> NewResponse(RequestOption option,
        IRequestItem? insertBefore = null, CancellationToken token = default);
}