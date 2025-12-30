using System.Windows;

namespace LLMClient.Component.CustomControl;

public class ExtendedWindow : Window
{
    public static readonly DependencyProperty BannerProperty = DependencyProperty.Register(
        nameof(Banner), typeof(object), typeof(ExtendedWindow), new PropertyMetadata(default(object)));

    public object Banner
    {
        get { return (object)GetValue(BannerProperty); }
        set { SetValue(BannerProperty, value); }
    }

    public static readonly DependencyProperty BannerMarginProperty = DependencyProperty.Register(
        nameof(BannerMargin), typeof(Thickness), typeof(ExtendedWindow), new PropertyMetadata(default(Thickness)));

    public Thickness BannerMargin
    {
        get { return (Thickness)GetValue(BannerMarginProperty); }
        set { SetValue(BannerMarginProperty, value); }
    }

    static ExtendedWindow()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ExtendedWindow),
            new FrameworkPropertyMetadata(typeof(ExtendedWindow)));
    }

    public ExtendedWindow()
    {
        this.Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var windowCaptionButtonWidth = SystemParameters.MinimizedGridWidth;
        this.BannerMargin = new Thickness(0, 0, windowCaptionButtonWidth, 0);
    }
}