using System.Windows;
using System.Windows.Controls;

namespace LLMClient.Dialog;

public partial class AgentOptionView : UserControl
{
    public AgentOptionView()
    {
        InitializeComponent();
    }

    public void SelectFolder_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AgentOption option) return;

        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Working Directory",
            SelectedPath = string.IsNullOrEmpty(option.WorkingDirectory)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : option.WorkingDirectory
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            option.WorkingDirectory = dialog.SelectedPath;
        }
    }
}
