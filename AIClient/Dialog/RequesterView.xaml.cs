using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LLMClient.Data;
using LLMClient.MCP;
using MaterialDesignThemes.Wpf;

namespace LLMClient.Dialog;

public partial class RequesterView : UserControl
{
    public RequesterView()
    {
        InitializeComponent();
    }

    private RequesterViewModel ViewModel => (RequesterViewModel)this.DataContext;

    private async void McpPopupBox_OnOpened(object sender, RoutedEventArgs e)
    {
        if (sender is PopupBox { PopupContent: AIFunctionTreeSelectorViewModel selector })
        {
            await selector.InitializeAsync();
        }
    }

    private void EnterKeyInputBinding_OnChecked(object sender, RoutedEventArgs e)
    {
        if (this.FindResource("PromptKeyBinding") is InputBinding inputBinding)
        {
            PromptTextBox.InputBindings.Add(inputBinding);
        }
    }

    private void EnterKeyInputBinding_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (this.FindResource("PromptKeyBinding") is InputBinding inputBinding)
        {
            PromptTextBox.InputBindings.Remove(inputBinding);
        }
    }

    private void RagPopupBox_OnOpened(object sender, RoutedEventArgs e)
    {
        ViewModel.RefreshRagSources();
    }

    private void PasteCommandBinding_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (Clipboard.ContainsFileDropList())
        {
            var fileDropList = Clipboard.GetFileDropList();
            foreach (var file in fileDropList)
            {
                if (string.IsNullOrEmpty(file))
                {
                    continue;
                }

                var extension = Path.GetExtension(file);
                if (!ImageExtensions.IsSupportedImageExtension(extension))
                {
                    continue;
                }

                var attachment = Attachment.CreateFromLocal(file, AttachmentType.Image);
                this.ViewModel.Attachments.Add(attachment);
            }
        }

        if (Clipboard.ContainsImage())
        {
            var attachment = Attachment.CreateFromClipBoards();
            if (attachment != null)
            {
                this.ViewModel.Attachments.Add(attachment);
            }
        }
        else if (Clipboard.ContainsText())
        {
            // 默认文本粘贴
            PromptTextBox.Paste();
        }
    }
}