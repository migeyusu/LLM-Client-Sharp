using System.Collections;
using System.Windows;
using System.Windows.Controls;
using LLMClient.Data;
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

    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(PromptEditor), new PropertyMetadata("System Prompt"));

    public string Header
    {
        get { return (string)GetValue(HeaderProperty); }
        set { SetValue(HeaderProperty, value); }
    }

    public void PromptEditorButton_OnClick(object sender, RoutedEventArgs e)
    {
        //这里必须通过resouce发送到dialoghost，否则会绑定异常
        var findResource = this.FindResource("HeaderedContentControl");
        if (findResource is FrameworkElement frameworkElement)
        {
            frameworkElement.DataContext = this;
            DialogHost.Show(frameworkElement);
        }
    }
}