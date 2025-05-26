using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
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

public class FlowDocumentScrollViewerEx : FlowDocumentScrollViewer
{
    public static readonly DependencyProperty CleanDocumentProperty = DependencyProperty.Register(
        nameof(CleanDocument), typeof(FlowDocument), typeof(FlowDocumentScrollViewerEx),
        new PropertyMetadata(default(FlowDocument), new PropertyChangedCallback(OnCleanDocumentChanged)));

    private static void OnCleanDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FlowDocumentScrollViewerEx viewer)
        {
            var oldDoc = e.OldValue as FlowDocument;
            var newDoc = e.NewValue as FlowDocument;

            // 先清除旧文档的引用
            if (oldDoc != null && viewer.Document == oldDoc)
            {
                viewer.Document = null;
            }

            // 如果新文档已经有父级，先从父级移除
            if (newDoc != null)
            {
                var parent = newDoc.Parent as FlowDocumentScrollViewerEx;
                if (parent != null && parent != viewer)
                {
                    parent.Document = null;
                }
            }

            // 设置新文档
            viewer.Document = newDoc;
        }
    }

    public FlowDocument CleanDocument
    {
        get { return (FlowDocument)GetValue(CleanDocumentProperty); }
        set { SetValue(CleanDocumentProperty, value); }
    }

    public FlowDocumentScrollViewerEx()
    {
    }


    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        e.Handled = false;
    }
}