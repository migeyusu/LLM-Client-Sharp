using System.IO;
using System.Windows;
using System.Windows.Input;
using LLMClient.UI.Component;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI;

public static class CommonCommands
{
    public interface ICopyable
    {
        /// <summary>
        /// 获取可复制的文本
        /// </summary>
        /// <returns></returns>
        string GetCopyText();
    }

    public static RoutedUICommand Exclude = new RoutedUICommand("Exclude", "Exclude", typeof(CommonCommands));

    public static RoutedCommand Clear = new RoutedUICommand("Clear", "Clear", typeof(CommonCommands));

    public static RoutedUICommand ReBase =
        new RoutedUICommand("Rebase", "Rebase", typeof(CommonCommands));

    public static RoutedUICommand Fork = new RoutedUICommand("Branch", "Branch", typeof(CommonCommands));

    public static RoutedUICommand ReprocessDialog =
        new RoutedUICommand("Reprocess", "Reprocess", typeof(CommonCommands));

    public static RoutedUICommand Backup = new RoutedUICommand("Backup", "Backup", typeof(CommonCommands));

    public static RoutedCommand Clone =
        new RoutedUICommand("Clone", "Clone", typeof(CommonCommands));

    public static RoutedCommand Conclusion =
        new RoutedUICommand("Conclusion", "Conclusion", typeof(CommonCommands));

    public static RoutedCommand OpenSource =
        new RoutedUICommand("OpenSource", "OpenSource", typeof(CommonCommands));

    private static ICommand? _openFileCommand;

    public static ICommand OpenFileCommand
    {
        get
        {
            return _openFileCommand ??= new ActionCommand((o =>
            {
                if (o is string filePath && !string.IsNullOrEmpty(filePath))
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        if (fileInfo.Exists)
                        {
                            System.Diagnostics.Process.Start("explorer.exe", fileInfo.FullName);
                        }
                        else
                        {
                            MessageBox.Show("File does not exist.");
                        }
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message);
                    }
                }
            }));
        }
    }

    private static ICommand? _copyCommand;

    public static ICommand CopyCommand
    {
        get
        {
            return _copyCommand ??= new ActionCommand((o =>
            {
                if (o is ICopyable copyable)
                {
                    var copyText = copyable.GetCopyText();
                    if (!string.IsNullOrEmpty(copyText))
                    {
                        try
                        {
                            Clipboard.SetText(copyText);
                            MessageEventBus.Publish("已复制文本到剪贴板");
                        }
                        catch (Exception e)
                        {
                            MessageBox.Show(e.Message);
                        }
                    }
                }
            }));
        }
    }

    private static ICommand? _exportCommand;

    public static ICommand ExportCommand
    {
        get
        {
            return _exportCommand ??= new ActionCommand((async o =>
            {
                if (o is string text && !string.IsNullOrEmpty(text))
                {
                    var fileDialog = new SaveFileDialog()
                    {
                        AddExtension = true,
                        OverwritePrompt = true, DefaultExt = ".md", Filter = "Markdown files (*.md)|*.md"
                    };
                    if (fileDialog.ShowDialog() == true)
                    {
                        using (var openFile = fileDialog.OpenFile())
                        {
                            using (var streamWriter = new StreamWriter(openFile))
                            {
                                await streamWriter.WriteAsync(text);
                            }
                        }
                    }
                }
            }));
        }
    }

    public static ICommand OpenFolderCommand => new ActionCommand(o =>
    {
        if (o is string path && !string.IsNullOrEmpty(path))
        {
            try
            {
                var directoryInfo = new DirectoryInfo(path);
                if (directoryInfo.Exists)
                {
                    System.Diagnostics.Process.Start("explorer.exe", directoryInfo.FullName);
                }
                else
                {
                    MessageBox.Show("Directory does not exist.");
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }
    });
}