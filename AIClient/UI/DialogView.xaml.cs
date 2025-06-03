using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using LLMClient.Abstraction;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient.UI;

public partial class DialogView : UserControl
{
    public DialogView()
    {
        InitializeComponent();
    }

    DialogViewModel ViewModel => (DialogViewModel)DataContext;

    private void OnDeleteExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is IDialogViewItem dialogViewItem)
        {
            ViewModel.DeleteItem(dialogViewItem);
        }
    }

    private void OnRedoExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is RequestViewItem requestViewItem)
        {
            ViewModel.ReBase(requestViewItem);
        }
    }

    private void OnExcludeExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is RequestViewItem requestViewItem)
        {
            ViewModel.InsertClearContextItem(requestViewItem);
        }
    }

    private void Refresh_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is MultiResponseViewItem multiResponseViewItem)
        {
            ViewModel.RetryCurrent(multiResponseViewItem);
        }
    }

    private void EnterKeyInputBinding_OnChecked(object sender, RoutedEventArgs e)
    {
        if (this.FindResource("PromptKeyBinding") is InputBinding findResource)
        {
            PromptTextBox.InputBindings.Add(findResource);
        }
    }

    private void EnterKeyInputBinding_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (this.FindResource("PromptKeyBinding") is InputBinding findResource)
        {
            PromptTextBox.InputBindings.Remove(findResource);
        }
    }

    private void Redo_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = ViewModel.Client != null;
    }

    private void PopupBox_OnOpened(object sender, RoutedEventArgs e)
    {
        if (sender is PopupBox popupBox)
        {
            var responseViewItem = popupBox.DataContext as MultiResponseViewItem;
            if (responseViewItem == null)
            {
                return;
            }

            var endpointService = BaseViewModel.ServiceLocator.GetService<IEndpointService>()!;
            popupBox.PopupContent = new ResponseAppendClientViewModel(responseViewItem, this.ViewModel, endpointService,
                async (client) => { await this.ViewModel.AppendResponseOn(responseViewItem, client); });
        }
    }

    private void Conclusion_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is MultiResponseViewItem item)
        {
            ViewModel.Conclusion(item);
        }
    }
}