using LLMClient.Abstraction;
using LLMClient.ToolCall;

namespace LLMClient.Dialog.Models;

public interface ITextDialogSession
{
    IReadOnlyList<IDialogItem> DialogItems { get; }

    List<IChatHistoryItem> GetHistory();

    Task CutContextAsync(IRequestItem? requestItem = null);
    
    string? SystemPrompt { get; }
    
    IEnumerable<Type> SupportedAgents { get; }

    IFunctionGroupSource? ToolsSource { get; }

    Task<IResponse> NewResponse(RequestOption option,
        IRequestItem? insertBefore = null, CancellationToken token = default);
}