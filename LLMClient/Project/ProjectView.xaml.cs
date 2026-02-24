using System.Windows.Controls;
using System.Windows.Input;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;

namespace LLMClient.Project;

public partial class ProjectView : UserControl
{
    public ProjectView()
    {
        InitializeComponent();
    }

    private void OnDeleteExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is IDialogItem dialogViewItem)
        {
            ViewModel.SelectedSession?.DeleteItem(dialogViewItem);
        }
    }

    private void OnExcludeExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is RequestViewItem requestViewItem)
        {
            ViewModel.SelectedSession?.CutContext(requestViewItem);
        }
    }

    ProjectViewModel ViewModel => (DataContext as ProjectViewModel)!;

    private void ConclusionBefore_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is RequestViewItem requestViewItem)
        {
            var indexOf = this.ViewModel.SelectedSession?.DialogItems.IndexOf(requestViewItem);
            if (indexOf <= 0)
            {
                return;
            }

            ViewModel.Requester.Summarize(requestViewItem);
        }
    }
    
}