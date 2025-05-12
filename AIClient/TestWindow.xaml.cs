using System.Windows;
using LLMClient.Endpoints;
using LLMClient.UI.Component;

namespace LLMClient;

public partial class TestWindow : Window
{
    public TestWindow()
    {
        InitializeComponent();
    }

    private void TestWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        Image.Source = Icons.GetIcon(ModelIconType.Deepseek);
        
    }
}