using System.IO;
using System.Windows;
using System.Windows.Controls;
using CefSharp.DevTools.Page;
using LLMClient.Component.Utility;
using LLMClient.Data;
using LLMClient.Dialog;
using LLMClient.Endpoints;
using LLMClient.Project;
using LLMClient.Rag;
using LLMClient.ToolCall;
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

    private async void SaveWorkspace_OnClick(object sender, RoutedEventArgs e)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        //save all workspace data like sessions, endpoints, research, projects, etc.
        var openFolderDialog = new OpenFolderDialog()
        {
            Multiselect = false,
        };
        if (openFolderDialog.ShowDialog() == true)
        {
            try
            {
                var folderName = openFolderDialog.FolderName;
                //copy files
                CopyRootFileIfExists(EndPointsConfig.EndPointsJsonFileName, folderName);
                CopyRootFileIfExists(GlobalOptions.DEFAULT_GLOBAL_CONFIG_FILE, folderName);
                CopyRootFileIfExists(McpServiceCollection.FileName, folderName);
                CopyRootFileIfExists(RagSourceCollection.ConfigFileName, folderName);
                CopyRootFileIfExists(PromptsResourceViewModel.PromptsFileName, folderName);
                //copy directory
                await CopyDirectoryIfExists(Path.Combine(currentDirectory, DialogFileViewModel.SaveFolder),
                    Path.Combine(folderName, DialogFileViewModel.SaveFolder));
                await CopyDirectoryIfExists(Path.Combine(currentDirectory, ProjectViewModel.SaveDir),
                    Path.Combine(folderName, ProjectViewModel.SaveDir));
                var tempPath = Extension.TempPath;
                await CopyDirectoryIfExists(tempPath, Path.Combine(folderName, Path.GetFileName(tempPath)));
                var imageAttachmentCacheFolder = Attachment.ImageAttachmentCacheFolder;
                await CopyDirectoryIfExists(imageAttachmentCacheFolder,
                    Path.Combine(folderName, Path.GetFileName(imageAttachmentCacheFolder)));
                await CopyDirectoryIfExists(Path.Combine(currentDirectory, Extension.CacheFolderName),
                    Path.Combine(folderName, Extension.CacheFolderName));
                MessageEventBus.Publish("已保存工作区配置文件");
            }
            catch (Exception exception)
            {
                MessageBox.Show("保存工作区配置文件失败: " + exception.Message);
            }
        }

        return;

        void CopyRootFileIfExists(string safeFileName, string targetDir)
        {
            var sourcePath = Path.Combine(currentDirectory, safeFileName);
            var targetPath = Path.Combine(targetDir, safeFileName);
            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, targetPath, true);
            }
        }
        
        Task CopyDirectoryIfExists(string sourceDir, string targetDir)
        {
            if (Directory.Exists(sourceDir))
            {
                return Extension.CopyDirectoryAsync(sourceDir, targetDir);
            }

            return Task.CompletedTask;
        }
    }
}