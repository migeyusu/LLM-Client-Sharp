using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace LLMClient.UI.Component;

public class ScrollViewerEx : ScrollViewer
{
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        e.Handled = false;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (this.GetTemplateChild("PART_HorizontalScrollBar") is ScrollBar templateChild1)
        {
            templateChild1.Visibility = Visibility.Collapsed;
        }

        if (this.GetTemplateChild("PART_VerticalScrollBar") is ScrollBar templateChild2)
        {
            templateChild2.Visibility = Visibility.Collapsed;
        }
    }
}