namespace LLMClient.Dialog.Models;

public interface ITextDialogSession
{
    IReadOnlyList<IDialogItem> DialogItems { get; }

    List<IChatHistoryItem> GetHistory();

    Task CutContextAsync(IRequestItem? requestItem = null);
    
    string? SystemPrompt { get; }
}