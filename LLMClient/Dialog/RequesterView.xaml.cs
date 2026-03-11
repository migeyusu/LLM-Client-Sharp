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