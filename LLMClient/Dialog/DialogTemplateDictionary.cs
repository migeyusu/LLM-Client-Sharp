using System.Windows;
using System.Windows.Controls;
using LLMClient.Component.UserControls;
using LLMClient.Dialog.Controls;
using LLMClient.Dialog.Models;
using MaterialDesignThemes.Wpf;

namespace LLMClient.Dialog;

public partial class DialogTemplateDictionary : ResourceDictionary
{
    private void ForkNavigation_PopupBox_OnOpened(object sender, RoutedEventArgs e)
    {
        if (sender is PopupBox popupBox)
        {
            if (popupBox is { DataContext: MultiResponseViewItem multiResponseViewItem, PopupContent: DialogGraphViewModel graphViewModel })
            {
                graphViewModel.LoadTree(multiResponseViewItem);
            }
        }
    }

    private void AppendModel_PopupBox_OnOpened(object sender, RoutedEventArgs e)
    {
        if (sender is PopupBox popupBox)
        {
            if (popupBox is
                {
                    PopupContent: ModelSelectionPopupViewModel popupViewModel,
                    DataContext: MultiResponseViewItem multiResponseViewItem
                })
            {
                popupViewModel.SuccessAction = model =>
                {
                    var llmChatClient = model.CreateClient();
                    multiResponseViewItem.AppendResponse(llmChatClient);
                };
            }
        }
    }

    private void TextBox_OnPreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.All;
            e.Handled = true;
        }
    }

    private void TextBox_OnDragEnter(object sender, DragEventArgs e)
    {
        ((Control)sender).BorderThickness = new Thickness(1);
    }

    private void TextBox_OnDragLeave(object sender, DragEventArgs e)
    {
        ((Control)sender).BorderThickness = new Thickness(0);
    }

    private void TextBox_OnDrop(object sender, DragEventArgs e)
    {
        var dataObject = e.Data;
        if (dataObject.GetDataPresent(DataFormats.FileDrop))
        {
            var data = e.Data.GetData(DataFormats.FileDrop, true);
            if (data is IEnumerable<string> paths)
            {
                if (((FrameworkElement)sender).DataContext is TextContentEditViewModel viewModel)
                {
                    viewModel.DropFiles(paths, sender);
                    e.Handled = true;
                }
            }
        }
    }
}