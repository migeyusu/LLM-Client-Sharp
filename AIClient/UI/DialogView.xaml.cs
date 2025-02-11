using System.Windows.Controls;
using System.Windows.Input;

namespace LLMClient.UI;

public partial class DialogView : UserControl
{
    public DialogView()
    {
        InitializeComponent();
    }

    private void OnDeleteExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (this.DataContext is DialogViewModel dialogViewModel && e.Parameter is IDialogViewItem dialogViewItem)
        {
            dialogViewModel.DeleteItem(dialogViewItem);
        }
    }
}