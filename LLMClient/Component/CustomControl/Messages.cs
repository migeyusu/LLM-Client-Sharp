using System.Windows;
using System.Windows.Threading;

namespace LLMClient.Component.CustomControl;

public class MessageBoxes
{
    private static MessageBoxResult Show(string message, string caption, MessageBoxButton button, MessageBoxImage icon)
    {
        if (CanUseCustomDialog())
        {
            var dispatcher = Application.Current!.Dispatcher;
            if (!dispatcher.CheckAccess())
            {
                return dispatcher.Invoke(() => Show(message, caption, button, icon));
            }

            var msgBox = new CustomMessageBoxWindow(message, caption, button, icon);
            msgBox.ShowDialog();
            return msgBox.Result;
        }

        return System.Windows.MessageBox.Show(message, caption, button, icon);
    }

    private static bool CanUseCustomDialog()
    {
        var application = Application.Current;
        if (application is not App)
        {
            return false;
        }

        var dispatcher = application.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return false;
        }

        if (!App.IsCustomMessageBoxReady)
        {
            return false;
        }

        return application.Resources.MergedDictionaries.Count > 0;
    }

    public static void Info(string message, string caption = "Information")
    {
        Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public static void Error(string message, string caption = "Error")
    {
        Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public static void Warning(string message, string caption = "Warning")
    {
        Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public static bool Question(string message, string caption = "Question")
    {
        return Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question)
               == MessageBoxResult.Yes;
    }
}