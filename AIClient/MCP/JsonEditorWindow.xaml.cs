using System.Windows;

namespace LLMClient.MCP;

public partial class JsonEditorWindow : Window
{
    public JsonEditorWindow()
    {
        InitializeComponent();
    }

    public string JsonContent
    {
        get => TextEditor.Text;
        set => TextEditor.Text = value;
    }

    private void OK_OnClick(object sender, RoutedEventArgs e)
    {
        this.DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        this.DialogResult = false;
    }
}