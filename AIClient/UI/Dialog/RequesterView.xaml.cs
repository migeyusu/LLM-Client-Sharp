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

    private void McpPopupBox_OnOpened(object sender, RoutedEventArgs e)
    {
        if (sender is PopupBox { PopupContent: AIFunctionSelectorViewModel selector })
        {
            selector.ResetSource();
            selector.EnsureAsync();
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
}