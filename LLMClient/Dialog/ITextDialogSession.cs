using System.Collections.ObjectModel;

namespace LLMClient.Dialog;

public interface ITextDialogSession
{
    string? SystemPrompt { get; set; }

    ObservableCollection<IDialogItem> DialogItems { get; }
}