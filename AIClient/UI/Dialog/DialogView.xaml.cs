using System.Collections;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.UI.MCP;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient.UI.Dialog;

public partial class DialogView : UserControl
{
    public DialogView()
    {
        InitializeComponent();
    }

    DialogViewModel ViewModel => (DialogViewModel)DataContext;

    private void OnDeleteExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is IDialogItem dialogViewItem)
        {
            ViewModel.DeleteItem(dialogViewItem);
        }
    }

    private void OnReBaseExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is RequestViewItem requestViewItem)
        {
            ViewModel.ReBaseOn(requestViewItem);
        }
    }

    private void OnExcludeExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is RequestViewItem requestViewItem)
        {
            ViewModel.CutContext(requestViewItem);
        }
    }
    
    private void ClearBefore_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is RequestViewItem requestViewItem)
        {
            ViewModel.ClearBefore(requestViewItem);
        }
    }
    
}