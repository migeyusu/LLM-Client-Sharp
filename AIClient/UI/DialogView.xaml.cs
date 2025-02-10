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
        var dialogViewModel = this.DataContext as DialogViewModel;
        dialogViewModel.DeleteItem(e.Parameter as IDialogViewItem);
    }
}