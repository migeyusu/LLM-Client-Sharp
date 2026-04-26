using Microsoft.Agents.AI;

namespace LLMClient.Dialog.Models;

public interface IDialogSession: IDialog
{
    /// <summary>
    /// session id
    /// </summary>
    Guid ID { get; }

    IResponseItem WorkingResponse { get; }

    IEnumerable<IDialogItem> GetChatHistory()
    {
        return WorkingResponse.GetChatHistory();
    }

    Task CutContextAsync(IRequestItem? requestItem = null);

    AIContextProvider[]? ContextProviders { get; }
    
    string? SystemPrompt { get; }
}