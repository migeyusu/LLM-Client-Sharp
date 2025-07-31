using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Wpf;

namespace LLMClient.Endpoints.OpenAIAPI;

public class APIModelInfoView : ContentControl
{
    public static readonly DependencyProperty UriProperty = DependencyProperty.Register(
        nameof(Uri), typeof(Uri), typeof(APIModelInfoView),
        new PropertyMetadata(default(Uri), new PropertyChangedCallback(PropertyChangedCallback)));

    private static void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is APIModelInfoView modelInfoView)
        {
            modelInfoView.UriChanged(e.NewValue as Uri);
        }
    }

    public Uri Uri
    {
        get { return (Uri)GetValue(UriProperty); }
        set { SetValue(UriProperty, value); }
    }

    public APIModelInfoView()
    {
        this.Loaded += APIModelInfoView_OnLoaded;
        this.Unloaded += APIModelInfoView_OnUnloaded;
    }
    /*
     * <wpf:WebView2 Source="{Binding InfoUri,Mode=OneWay}"
                      Visibility="{Binding InfoUrl,Converter={x:Static materialDesign:NullableToVisibilityConverter.CollapsedInstance}}"
        >
            <!--<wpf:WebView2.CreationProperties>
                            <wpf:CoreWebView2CreationProperties  ></wpf:CoreWebView2CreationProperties>
                        </wpf:WebView2.CreationProperties>-->
        </wpf:WebView2>
     */

    private static readonly Lazy<WebView2> LazyWebView = new Lazy<WebView2>(() => new WebView2());

    private void UriChanged(Uri? source)
    {
        if (source == null)
        {
            return;
        }

        if (this.Content is WebView2 view2)
        {
            view2.Source = source;
        }
    }

    private void APIModelInfoView_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (this.Content == null)
        {
            var webView2 = LazyWebView.Value;
            webView2.Source = this.Uri;
            this.Content = webView2;
        }
    }

    private void APIModelInfoView_OnUnloaded(object sender, RoutedEventArgs e)
    {
        this.Content = null;
    }
}