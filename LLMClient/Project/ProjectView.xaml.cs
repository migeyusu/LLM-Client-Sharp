using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LLMClient.Component.ViewModel;
using LLMClient.Dialog;

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
            ViewModel.SelectedTask?.DeleteItem(dialogViewItem);
        }
    }

    private void OnExcludeExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is RequestViewItem requestViewItem)
        {
            ViewModel.SelectedTask?.CutContext(requestViewItem);
        }
    }

    private void ClearBefore_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is RequestViewItem requestViewItem)
        {
            ViewModel.SelectedTask?.ClearBefore(requestViewItem);
        }
    }

    ProjectViewModel ViewModel => (DataContext as ProjectViewModel)!;

    private void TaskMoveLeft_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is ProjectTaskViewModel task)
        {
            var i = ViewModel.Tasks.IndexOf(task);
            if (i > 0)
            {
                ViewModel.Tasks.Move(i, i - 1);
            }
        }
    }

    private void TaskDelete_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is ProjectTaskViewModel task)
        {
            ViewModel.RemoveTask(task);
        }
    }

    private void ConclusionBefore_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is RequestViewItem requestViewItem)
        {
            var indexOf = this.ViewModel.SelectedTask?.DialogItems.IndexOf(requestViewItem);
            if (indexOf <= 0)
            {
                return;
            }

            ViewModel.Requester.Summarize();
        }
    }


    /// <summary>
    /// 基于当前对话创建分支
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ProjectBranch_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is IDialogItem dialogViewItem)
        {
            ViewModel.ForkPreTask(dialogViewItem);
        }

        e.Handled = true;
    }
}