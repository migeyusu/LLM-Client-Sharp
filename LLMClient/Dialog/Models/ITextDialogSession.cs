using LLMClient.Abstraction;
using LLMClient.ToolCall;
using Microsoft.Agents.AI;

namespace LLMClient.Dialog.Models;

public interface ITextDialogSession
{
    Guid ID { get; }
    
    IReadOnlyList<IDialogItem> DialogItems { get; }

    List<IChatHistoryItem> GetHistory();
    
    

    Task CutContextAsync(IRequestItem? requestItem = null);
    
    AIContextProvider[]? ContextProviders { get; }
    
    string? SystemPrompt { get; }
    
    IEnumerable<Type> SupportedAgents { get; }

    IFunctionGroupSource? ToolsSource { get; }

    Task<IResponse> NewResponse(RequestOption option,
        IRequestItem? insertBefore = null, CancellationToken token = default);
}