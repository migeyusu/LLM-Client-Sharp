using System.Windows.Controls;

namespace LLMClient.Component.CustomControl;

public class CustomVirtualizingStackPanel : VirtualizingStackPanel
{
    /*protected override void OnCleanUpVirtualizedItem(CleanUpVirtualizedItemEventArgs e)
    {
        base.OnCleanUpVirtualizedItem(e);
        if (e.Value is MultiResponseViewItem)
        {
            var richTextBox = e.UIElement.FindVisualChild<FlowDocumentScrollViewer>();
            if (richTextBox is { Document: not null })
            {
                richTextBox.Document = new FlowDocument(); // 清除 FlowDocument 的引用
            }
        }
    }*/

    /*protected override void OnClearChildren()
    {
        if (this.IsItemsHost)
        {
            var info = typeof(VirtualizingStackPanel).GetProperty("RealizedChildren",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var value = info?.GetValue(this) as IList;
            if (value == null)
            {
                return;
            }

            foreach (ListBoxItem item in value)
            {
                var richTextBox = item.FindVisualChild<FlowDocumentScrollViewer>();
                if (richTextBox is { Document: not null })
                {
                    richTextBox.Document = new FlowDocument(); // 清除 FlowDocument 的引用
                }
            }
        }

        base.OnClearChildren();
    }*/
}