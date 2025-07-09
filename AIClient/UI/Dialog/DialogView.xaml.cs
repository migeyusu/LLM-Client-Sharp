using System.Collections;
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

    private void OnRedoExecuted(object sender, ExecutedRoutedEventArgs e)
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

    private void ModelComparePopupBox_OnOpened(object sender, RoutedEventArgs e)
    {
        if (sender is PopupBox popupBox)
        {
            var responseViewItem = popupBox.DataContext as MultiResponseViewItem;
            if (responseViewItem == null)
            {
                return;
            }

            var endpointService = BaseViewModel.ServiceLocator.GetService<IEndpointService>()!;
            popupBox.PopupContent = new PopupModelSelectionViewModel(endpointService,
                async (selector) =>
                {
                    var client = selector.GetClient();
                    if (client == null)
                    {
                        return;
                    }

                    await this.ViewModel.AppendResponseOn(responseViewItem, client);
                });
        }
    }

    private void Conclusion_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        ViewModel.ConclusionCurrent();
    }

    private void ClearBefore_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is RequestViewItem requestViewItem)
        {
            ViewModel.ClearBefore(requestViewItem);
        }
    }

    private async void McpPopupBox_OnOpened(object sender, RoutedEventArgs e)
    {
        if (sender is PopupBox popupBox)
        {
            var selectedFunctions = ViewModel.SelectedFunctions;
            var selectionViewModel =
                new AIFunctionSelectionViewModel(selectedFunctions ?? Array.Empty<IAIFunctionGroup>(), false);
            foreach (var aiFunctionGroup in selectionViewModel.FunctionCollection)
            {
                await aiFunctionGroup.Data.EnsureAsync(CancellationToken.None);
            }

            popupBox.PopupContent = selectionViewModel;
        }
    }

    private void McpPopupBox_OnClosed(object sender, RoutedEventArgs e)
    {
        if (sender is PopupBox popupBox)
        {
            var selectionViewModel = popupBox.PopupContent as AIFunctionSelectionViewModel;
            this.ViewModel.SelectedFunctions =
                selectionViewModel?.FunctionCollection
                    .Where((group => group.IsSelected))
                    .Select((model => model.Data)).ToArray();
        }
    }
}