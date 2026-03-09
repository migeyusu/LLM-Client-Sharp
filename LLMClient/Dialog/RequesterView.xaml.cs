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

    private void PromptBox_OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Move;
        }

        e.Handled = true;
    }

    private async void PromptBox_OnDrop(object sender, DragEventArgs e)
    {
        var dataObject = e.Data;
        if (dataObject.GetDataPresent(DataFormats.FileDrop))
        {
            var data = e.Data.GetData(DataFormats.FileDrop, true);
            if (data is IEnumerable<string> paths)
            {
                var stringBuilder = new StringBuilder();
                foreach (var path in paths)
                {
                    if (File.Exists(path))
                    {
                        var readAllText = await File.ReadAllTextAsync(path);
                        stringBuilder.AppendLine(readAllText);
                    }
                }

                if (this.DataContext is RequesterViewModel requesterViewModel)
                {
                    requesterViewModel.PromptEditViewModel.AppendTempText(stringBuilder.ToString());
                }
            }
        }
    }

    private void UIElement_OnDragLeave(object sender, DragEventArgs e)
    {
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