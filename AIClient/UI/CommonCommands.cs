using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI;

public class CommonCommands
{
    private static ICommand? _copyCommand;

    public static ICommand CopyCommand
    {
        get
        {
            return _copyCommand ??= new ActionCommand((o =>
            {
                if (o is string text && !string.IsNullOrEmpty(text))
                {
                    Clipboard.SetText(text);
                }
            }));
        }
    }
}