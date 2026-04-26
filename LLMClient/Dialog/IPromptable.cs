using System.Collections.ObjectModel;
using LLMClient.Configuration;

namespace LLMClient.Dialog;

public interface IPromptable
{
    string? UserSystemPrompt { get; set; }

    ObservableCollection<PromptEntry> ExtendedSystemPrompts { get; }
}