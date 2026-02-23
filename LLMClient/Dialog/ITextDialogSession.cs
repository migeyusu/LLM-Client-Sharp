using LLMClient.Dialog.Models;

namespace LLMClient.Dialog;

public interface ITextDialogSession
{
    string? SystemPrompt { get; }

    IReadOnlyList<IDialogItem> DialogItems { get; }
}