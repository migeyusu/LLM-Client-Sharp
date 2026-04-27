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
        if (DataContext is not AgentConfig agentConfig) return;

        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Working Directory",
            SelectedPath = string.IsNullOrEmpty(agentConfig.WorkingDirectory)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : agentConfig.WorkingDirectory
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            agentConfig.WorkingDirectory = dialog.SelectedPath;
        }
    }
}
