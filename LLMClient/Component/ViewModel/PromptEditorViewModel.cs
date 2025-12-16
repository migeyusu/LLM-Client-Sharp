using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Configuration;
using LLMClient.Data;
using LLMClient.Dialog;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient.Component.ViewModel;

public class PromptEditorViewModel : BaseViewModel
{
    public PromptEditorViewModel(IPromptableSession session)
    {
        Session = session;
        SelectablePrompts = ServiceLocator.GetService<IPromptsResource>()?
            .SystemPrompts
            .Select(entry => new SelectableViewModel<PromptEntry>(entry))
            .ToList() ?? [];
        var promptEntries = session.ExtendedSystemPrompts;
        if (promptEntries.Any() && SelectablePrompts.Any())
        {
            foreach (var promptEntry in promptEntries)
            {
                SelectableViewModel<PromptEntry>? item = null;
                if ((item = SelectablePrompts.FirstOrDefault(entry => entry.Data.Equals(promptEntry))) != null)
                {
                    item.IsSelected = true;
                }
            }
        }

        AddCommand = new RelayCommand<SelectableViewModel<PromptEntry>>(((item) =>
        {
            if (item != null)
            {
                this.Session.ExtendedSystemPrompts.Add(item.Data);
                item.IsSelected = true;
            }
        }));
        RemoveCommand = new RelayCommand<PromptEntry>((item =>
        {
            if (item == null) return;
            Session.ExtendedSystemPrompts.Remove(item);
            var @default = SelectablePrompts.FirstOrDefault(model => Equals(model.Data, item));
            if (@default != null)
            {
                @default.IsSelected = false;
            }
        }));
    }

    public List<SelectableViewModel<PromptEntry>> SelectablePrompts { get; }

    public IPromptableSession Session { get; }

    public ICommand AddCommand { get; }

    public ICommand RemoveCommand { get; }
}