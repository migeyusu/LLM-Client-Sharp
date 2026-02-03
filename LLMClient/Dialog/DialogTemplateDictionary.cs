using System.Windows;
using LLMClient.Dialog.Controls;
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
                    multiResponseViewItem.NewRequest(llmChatClient);
                };
            }
        }
    }
}