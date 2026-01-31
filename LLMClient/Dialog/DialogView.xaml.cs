using System.Windows.Controls;
using System.Windows.Input;

namespace LLMClient.Dialog;

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

    private void ConclusionBefore_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is RequestViewItem requestViewItem)
        {
            var indexOf = this.ViewModel.DialogItems.IndexOf(requestViewItem);
            if (indexOf <= 0)
            {
                return;
            }

            ViewModel.Requester.Summarize(requestViewItem);
        }
    }
}