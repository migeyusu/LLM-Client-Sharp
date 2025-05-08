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
            CommonCommands.CopyCommand.Execute(textBlock.Text);
            MessageEventBus.Publish("已复制");
        }
    }

    private void CommandBinding_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.OriginalSource is ListBoxItem listBoxItem)
        {
            CommonCommands.CopyCommand.Execute(listBoxItem.Content);
        }
    }
}