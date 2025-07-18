using System.Windows.Controls;
using System.Windows.Input;

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