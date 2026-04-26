using LLMClient.Abstraction;
using LLMClient.ToolCall;

namespace LLMClient.Dialog.Models;

public interface IDialog
{
    IReadOnlyList<IDialogItem> VisualDialogItems { get; }
    
    IFunctionGroupSource? ToolsSource { get; }
    
    IPromptCommandAggregate? PromptCommand { get; }
    
    IEnumerable<Type> SupportedAgents { get; }
    
    Task<IResponse> NewResponse(RequestOption option,
        IRequestItem? insertBefore = null, CancellationToken token = default);
}