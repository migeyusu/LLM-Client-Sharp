using System.Windows.Controls;
using System.Windows.Input;
using LLMClient.Component.Utility;
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
        try
        {
            if (e.Parameter is IDialogItem dialogViewItem)
            {
                ViewModel.DeleteItem(dialogViewItem);
            }
        }
        catch (Exception exception)
        {
            MessageEventBus.Publish($"删除失败：{exception.Message}");
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