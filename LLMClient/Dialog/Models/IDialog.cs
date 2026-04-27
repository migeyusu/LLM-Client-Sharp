using LLMClient.Abstraction;
using LLMClient.ToolCall;

namespace LLMClient.Dialog.Models;

public interface IDialogSession : ISession
{
    IReadOnlyList<IDialogItem> VisualDialogItems { get; }

    IFunctionGroupSource? ToolsSource { get; }

    IEnumerable<Type> SupportedAgents { get; }

    Task CutContextAsync(IRequestItem? requestItem = null);

    Task<IResponse> NewResponse(RequestOption option,
        IRequestItem? insertBefore = null, CancellationToken token = default);
}