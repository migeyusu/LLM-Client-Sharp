using System.Windows.Input;
using LLMClient.Dialog;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Project;

/// <summary>
/// ViewModel for the dialog selection popup when importing dialog sessions into a project.
/// </summary>
public class ImportDialogSelectionViewModel
{
    public IReadOnlyList<DialogViewModel> DialogSessions { get; }

    public ICommand SelectCommand { get; } = new ActionCommand(o =>
    {
        if (o is DialogViewModel dialog)
        {
            DialogHost.CloseDialogCommand.Execute(dialog, null);
        }
    });

    public ImportDialogSelectionViewModel(IReadOnlyList<DialogViewModel> dialogSessions)
    {
        DialogSessions = dialogSessions;
    }
}

