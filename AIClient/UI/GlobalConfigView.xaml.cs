using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace LLMClient.UI;

public partial class GlobalConfigView : UserControl
{
    public GlobalConfigView()
    {
        InitializeComponent();
    }

    private void OpenDialogs_OnClick(object sender, RoutedEventArgs e)
    {
        var path = Path.GetFullPath(MainViewModel.DialogSaveFolder);
        var directoryInfo = new DirectoryInfo(path);
        if (!directoryInfo.Exists)
        {
            return;
        }

        Process.Start("explorer.exe", path);
    }
}