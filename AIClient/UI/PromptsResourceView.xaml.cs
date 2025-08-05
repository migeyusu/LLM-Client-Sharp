using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LLMClient.UI.Component;

namespace LLMClient.UI;

public partial class PromptsResourceView : UserControl
{
    public PromptsResourceView()
    {
        InitializeComponent();
    }

    private void ListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is TextBlock textBlock)
        {
            try
            {
                Clipboard.SetText(textBlock.Text);
                MessageEventBus.Publish("已复制");
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }
        }
    }
}