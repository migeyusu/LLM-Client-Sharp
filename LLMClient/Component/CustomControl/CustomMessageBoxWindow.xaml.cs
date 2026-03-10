using System.Windows;
using MaterialDesignThemes.Wpf;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Input; // Added using directive for MouseButtonEventArgs and MouseButton

namespace LLMClient.Component.CustomControl;

public partial class CustomMessageBoxWindow : Window
{
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    public CustomMessageBoxWindow(string message, string caption, MessageBoxButton button, MessageBoxImage icon)
    {
        InitializeComponent();
        
        // Ensure we are on the UI thread and set owner if possible
        if (Application.Current != null && Application.Current.MainWindow != null && Application.Current.MainWindow.IsLoaded)
        {
            this.Owner = Application.Current.MainWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        
        this.Title = caption;
        this.TitleTextBlock.Text = caption;
        this.MessageTextBlock.Text = message;
        ConfigureButtons(button);
        ConfigureIcon(icon);
    }

    private void ConfigureButtons(MessageBoxButton button)
    {
        // Reset visibility
        OkButton.Visibility = Visibility.Collapsed;
        YesButton.Visibility = Visibility.Collapsed;
        NoButton.Visibility = Visibility.Collapsed;
        CancelButton.Visibility = Visibility.Collapsed;

        switch (button)
        {
            case MessageBoxButton.OK:
                OkButton.Visibility = Visibility.Visible;
                OkButton.IsDefault = true;
                break;
            case MessageBoxButton.OKCancel:
                OkButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Visible;
                OkButton.IsDefault = true;
                CancelButton.IsCancel = true;
                break;
            case MessageBoxButton.YesNo:
                YesButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Visible;
                YesButton.IsDefault = true;
                NoButton.IsCancel = true; // No acts as cancel in Yes/No dialog
                break;
            case MessageBoxButton.YesNoCancel:
                YesButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Visible;
                YesButton.IsDefault = true;
                CancelButton.IsCancel = true;
                break;
        }
    }

    private void ConfigureIcon(MessageBoxImage icon)
    {
        IconControl.Visibility = Visibility.Visible;
        switch (icon)
        {
            case MessageBoxImage.None:
                 IconControl.Visibility = Visibility.Collapsed;
                 break;
            case MessageBoxImage.Information: // Information (Default)
                IconControl.Kind = PackIconKind.Information;
                break;
            case MessageBoxImage.Error: // Error, Hand, Stop
                IconControl.Kind = PackIconKind.AlertCircle; // Error icon
                IconControl.Foreground = Brushes.Red;
                break;
            case MessageBoxImage.Warning: // Warning, Exclamation
                IconControl.Kind = PackIconKind.Alert; // Warning icon
                IconControl.Foreground = Brushes.Orange;
                break;
            case MessageBoxImage.Question: // Question
                IconControl.Kind = PackIconKind.QuestionMarkCircle;
                break;
            default:
                IconControl.Kind = PackIconKind.Information;
                break;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.OK;
        Close();
    }

    private void YesButton_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.Yes;
        Close();
    }

    private void NoButton_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.No;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.Cancel;
        Close();
    }

    private void Title_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            this.DragMove();
        }
    }
}
