using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI;

public static class CommonCommands
{
    public static RoutedUICommand Exclude = new RoutedUICommand("Exclude", "Exclude", typeof(CommonCommands));

    public static RoutedCommand Clear = new RoutedUICommand("Clear", "Clear", typeof(CommonCommands));
    
    public static RoutedUICommand Conclusion =
        new RoutedUICommand("Conclusion", "Conclusion", typeof(CommonCommands));

    public static RoutedUICommand Branch = new RoutedUICommand("Branch", "Branch", typeof(CommonCommands));

    public static RoutedUICommand ReprocessDialog = new RoutedUICommand("Reprocess", "Reprocess", typeof(CommonCommands));

    private static ICommand? _copyCommand;

    public static ICommand CopyCommand
    {
        get
        {
            return _copyCommand ??= new ActionCommand((o =>
            {
                if (o is string text && !string.IsNullOrEmpty(text))
                {
                    try
                    {
                        Clipboard.SetText(text);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message);
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
}