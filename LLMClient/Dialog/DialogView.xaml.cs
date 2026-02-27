using System.Windows.Controls;
using System.Windows.Input;
using LLMClient.Dialog.Models;

namespace LLMClient.Dialog;

public partial class DialogView : UserControl
{
    public DialogView()
    {
        InitializeComponent();
    }

    private DialogViewModel ViewModel => (DialogViewModel)DataContext;

    private void FindCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
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