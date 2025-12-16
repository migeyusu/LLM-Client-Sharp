using System.Collections.ObjectModel;
using LLMClient.Configuration;

namespace LLMClient.Dialog;

public interface ITextDialogSession
{
    string? SystemPrompt { get; }

    ObservableCollection<IDialogItem> DialogItems { get; }
}

public interface IPromptableSession
{
    string? UserSystemPrompt { get; set; }

    ObservableCollection<PromptEntry> ExtendedSystemPrompts { get; }
}