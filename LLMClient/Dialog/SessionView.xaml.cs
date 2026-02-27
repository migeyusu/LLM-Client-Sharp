using System.Windows.Controls;
using System.Windows.Input;
using LLMClient.Dialog.Models;

namespace LLMClient.Dialog;

public partial class SessionView : UserControl
{
    public SessionView()
    {
        InitializeComponent();
    }

    private DialogSessionViewModel ViewModel => (DialogSessionViewModel)DataContext;

    private void OnDeleteExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is IDialogItem dialogViewItem)
        {
            ViewModel.DeleteItem(dialogViewItem);
        }
    }

    private void OnExcludeExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is RequestViewItem requestViewItem)
        {
            ViewModel.CutContext(requestViewItem);
        }
    }
}