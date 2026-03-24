namespace LLMClient.Dialog.Models;

public interface ITextDialogSession
{
    IReadOnlyList<IDialogItem> DialogItems { get; }

    List<IChatHistoryItem> GetHistory();
    
    string? SystemPrompt { get; }
}