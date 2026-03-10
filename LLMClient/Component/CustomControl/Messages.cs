using System.Windows;

namespace LLMClient.Component.CustomControl;

public class MessageBoxes
{
    public static void Info(string message, string caption = "Information")
    {
        MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public static void Error(string message, string caption = "Error")
    {
        MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public static void Warning(string message, string caption = "Warning")
    {
        MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public static bool Question(string message, string caption = "Question")
    {
        return MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question)
               == MessageBoxResult.Yes;
    }
}