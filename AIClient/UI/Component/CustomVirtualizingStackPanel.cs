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
            var richTextBox = FindControlInContainer<FlowDocumentScrollViewer>(e.UIElement);
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
                var richTextBox = FindControlInContainer<FlowDocumentScrollViewer>(item);
                if (richTextBox is { Document: not null })
                {
                    richTextBox.Document = new FlowDocument(); // 清除 FlowDocument 的引用
                }
            }
        }

        base.OnClearChildren();
    }

    /// <summary>
    /// 从 ListBoxItem 中查找 RichTextBox 控件
    /// </summary>
    private T? FindControlInContainer<T>(DependencyObject container) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(container); i++)
        {
            var child = VisualTreeHelper.GetChild(container, i);

            if (child is T richTextBox)
            {
                return richTextBox;
            }

            var result = FindControlInContainer<T>(child);
            if (result != null) return result;
        }

        return null; // 没有找到 RichTextBox
    }
}