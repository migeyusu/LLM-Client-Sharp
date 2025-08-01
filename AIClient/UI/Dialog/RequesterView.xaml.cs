using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LLMClient.UI.MCP;
using MaterialDesignThemes.Wpf;

namespace LLMClient.UI.Dialog;

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

    private void SearchPopupBox_OnOpened(object sender, RoutedEventArgs e)
    {
    }
}