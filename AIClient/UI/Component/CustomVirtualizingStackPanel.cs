using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace LLMClient.UI.Component;

public class CustomVirtualizingStackPanel : VirtualizingStackPanel
{
    protected override void OnCleanUpVirtualizedItem(CleanUpVirtualizedItemEventArgs e)
    {
        base.OnCleanUpVirtualizedItem(e);
        if (e.Value is ResponseViewItem)
        {
            var richTextBox = e.UIElement.FindVisualChild<FlowDocumentScrollViewer>();
            if (richTextBox is { Document: not null })
            {
                richTextBox.Document = new FlowDocument(); // 清除 FlowDocument 的引用
            }
        }
    }

    protected override void OnClearChildren()
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
    }
}