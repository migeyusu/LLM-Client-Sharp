using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using LLMClient.Endpoints;
using LLMClient.UI.Component;
using Microsoft.Win32;

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

    private async void ImportEndpoints_OnClick(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog()
        {
            Filter = "Endpoints (EndPoints.json)|EndPoints.json"
        };
        if (openFileDialog.ShowDialog() == true)
        {
            var fileName = openFileDialog.FileName;
            var tryLoadDoc = await EndPointsConfiguration.TryLoadDoc(fileName);
            if (tryLoadDoc == null)
            {
                MessageBox.Show("导入失败，文件格式不正确或文件不存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var fullPath = Path.GetFullPath(EndPointsConfiguration.EndPointsJsonFileName);
            File.Copy(fullPath, fullPath + ".bak", true);
            File.Copy(fileName, fullPath, true);
            MessageEventBus.Publish("导入成功，已覆盖当前配置。请重启应用程序以使更改生效。");
        }
    }
}