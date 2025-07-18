﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LLMClient.UI.Dialog;

namespace LLMClient.UI.Project;

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
        if (e.Parameter is ProjectTask task)
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
        if (e.Parameter is ProjectTask task)
        {
            ViewModel.RemoveTask(task);
        }
    }
}