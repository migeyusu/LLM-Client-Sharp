using System.Collections.ObjectModel;

namespace LLMClient.Dialog;

public interface ITextDialogSession
{
    string? SystemPrompt { get; }

    ObservableCollection<IDialogItem> DialogItems { get; }
}