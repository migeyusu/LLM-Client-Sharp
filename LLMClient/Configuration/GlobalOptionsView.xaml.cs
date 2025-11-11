using System.IO;
using System.Windows;
using System.Windows.Controls;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.UI.Component.Utility;
using Microsoft.Win32;

namespace LLMClient.Configuration;

public partial class GlobalOptionsView : UserControl
{
    public GlobalOptionsView()
    {
        InitializeComponent();
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
            var tryLoadDoc = await EndPointsConfig.TryLoadDoc(fileName);
            if (tryLoadDoc == null)
            {
                MessageBox.Show("导入失败，文件格式不正确或文件不存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var fullPath = Path.GetFullPath(EndPointsConfig.EndPointsJsonFileName);
            File.Copy(fullPath, fullPath + ".bak", true);
            File.Copy(fileName, fullPath, true);
            MessageEventBus.Publish("导入成功，已覆盖当前配置。请重启应用程序以使更改生效。");
        }
    }

    private void ClearHttpCache_OnClick(object sender, RoutedEventArgs e)
    {
        HttpContentCache.Instance.Clear();
        MessageEventBus.Publish("已清空");
    }
}