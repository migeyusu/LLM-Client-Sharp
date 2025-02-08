using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;

namespace LLMClient.UI;

public class CustomVirtualizingStackPanel : VirtualizingStackPanel
{
    // IRecyclingItemContainerGenerator


    protected override void OnCleanUpVirtualizedItem(CleanUpVirtualizedItemEventArgs e)
    {
        base.OnCleanUpVirtualizedItem(e);
        if (e.Value is ResponseViewItem viewItem)
        {
            var richTextBox = FindRichTextBoxInContainer(e.UIElement);
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
                var richTextBox = FindRichTextBoxInContainer(item);
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
    private RichTextBox? FindRichTextBoxInContainer(DependencyObject container)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(container); i++)
        {
            var child = VisualTreeHelper.GetChild(container, i);

            if (child is RichTextBox richTextBox)
            {
                return richTextBox;
            }

            var result = FindRichTextBoxInContainer(child);
            if (result != null) return result;
        }

        return null; // 没有找到 RichTextBox
    }
}