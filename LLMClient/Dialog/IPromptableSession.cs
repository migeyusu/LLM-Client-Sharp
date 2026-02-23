using System.Collections.ObjectModel;
using LLMClient.Configuration;

namespace LLMClient.Dialog;

public interface IPromptableSession
{
    string? UserSystemPrompt { get; set; }

    ObservableCollection<PromptEntry> ExtendedSystemPrompts { get; }
}