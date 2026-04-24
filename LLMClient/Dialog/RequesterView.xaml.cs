using System.Windows;
using System.Windows.Controls;

using LLMClient.ToolCall;
using MaterialDesignThemes.Wpf;
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

    private void RequestModePopup_OnOpened(object sender, EventArgs e)
    {
        AgentModeListBox.SelectedItem = ViewModel.IsAgentMode
            ? ViewModel.SelectedAgent
            : null;
    }


    private void DialogModeButton_OnClick(object sender, RoutedEventArgs e)
    {
        AgentModeListBox.SelectedItem = null;

        ViewModel.IsAgentMode = false;
        RequestModeToggleButton.IsChecked = false;
    }

    private void AgentModeListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox { SelectedItem: AgentDescriptor agent })
        {
            return;
        }

        ViewModel.SelectedAgent = agent;
        ViewModel.IsAgentMode = true;
        RequestModeToggleButton.IsChecked = false;
    }

    private async void PromptEditor_OnLostKeyboardFocus(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            await this.ViewModel.PromptEditViewModel.ApplyText();
            this.ViewModel.InvalidateAsyncProperty(nameof(RequesterViewModel.EstimatedTokens));
        }
    }

    private async void SkillsPopupBox_OnOpened(object sender, RoutedEventArgs e)
    {
        if (sender is PopupBox { PopupContent: SkillsListViewModel skillsList })
        {
            await skillsList.InitializeAsync();
        }
    }
}