using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using LLMClient.Data;
using LLMClient.Dialog.Models;
using LLMClient.ToolCall;
using MaterialDesignThemes.Wpf;
using Clipboard = System.Windows.Clipboard;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using UserControl = System.Windows.Controls.UserControl;

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
        else if (Clipboard.ContainsText() && sender is TextBoxBase textBoxBase)
        {
            // 默认文本粘贴
            textBoxBase.Paste();
        }
    }

    private void RagPopupBox_OnClosed(object sender, RoutedEventArgs e)
    {
        ViewModel.NotifyRagSelection();
    }

    private async void PromptEditor_OnLostKeyboardFocus(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            await this.ViewModel.PromptEditViewModel.ApplyText();
            this.ViewModel.InvalidateAsyncProperty(nameof(RequesterViewModel.EstimatedTokens));
        }
    }
}