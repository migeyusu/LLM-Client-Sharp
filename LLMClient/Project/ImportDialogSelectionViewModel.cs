using System.Windows.Input;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Dialog;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Project;

/// <summary>
/// ViewModel for the dialog selection popup when importing dialog sessions into a project.
/// </summary>
public class ImportDialogSelectionViewModel : BaseViewModel
{
    private DialogViewModel? _selectedSession;

    public IReadOnlyList<DialogViewModel> DialogSessions { get; }

    public DialogViewModel? SelectedSession
    {
        get => _selectedSession;
        set => SetField(ref _selectedSession, value);
    }

    public ICommand SelectCommand { get; }

    public ImportDialogSelectionViewModel(IReadOnlyList<DialogViewModel> dialogSessions)
    {
        DialogSessions = dialogSessions;
        SelectCommand = new ActionCommand(_ =>
        {
            if (SelectedSession is { } dialog)
            {
                DialogHost.CloseDialogCommand.Execute(dialog, null);
            }
        });
    }
}

