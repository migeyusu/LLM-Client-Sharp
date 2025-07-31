using System.Collections;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;

namespace LLMClient.UI.Component;

public partial class PromptEditor : UserControl
{
    public PromptEditor()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty PromptStringProperty = DependencyProperty.Register(
        nameof(PromptString), typeof(string), typeof(PromptEditor),
        new PropertyMetadata(default(string)));

    public string PromptString
    {
        set { SetValue(PromptStringProperty, value); }
        get { return (string)GetValue(PromptStringProperty); }
    }

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(IList), typeof(PromptEditor), new PropertyMetadata(default(IList)));

    public IList Source
    {
        get { return (IList)GetValue(SourceProperty); }
        set { SetValue(SourceProperty, value); }
    }

    public void Button_OnClick(object sender, RoutedEventArgs e)
    {
        var findResource = this.FindResource("HeaderedContentControl");
        if (findResource is FrameworkElement frameworkElement)
        {
            frameworkElement.DataContext = this;
            DialogHost.Show(frameworkElement);
        }
    }
}