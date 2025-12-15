using System.Collections;
using System.Windows;
using LLMClient.Component.ViewModel.Base;
using MaterialDesignThemes.Wpf;

namespace LLMClient.Component.UserControls;

public partial class PromptEditor
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

public class PromptEditorViewModel : BaseViewModel
{
    
    
    public string GetPromptString()
    {
        
    }


}