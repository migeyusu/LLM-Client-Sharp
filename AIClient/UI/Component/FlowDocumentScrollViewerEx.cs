using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace LLMClient.UI.Component;

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
            if (newDoc?.Parent is FlowDocumentScrollViewerEx parent && parent != viewer)
            {
                parent.Document = null;
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

    private static readonly Lazy<Brush> DefaultHighlightBrushLazy = new Lazy<Brush>(() =>
    {
        // 使用默认的高亮颜色
        var solidColorBrush = new SolidColorBrush(Color.FromArgb(64, 255, 255, 0));
        solidColorBrush.Freeze();
        return solidColorBrush;
    });

    private static Brush DefaultHighlightBrush => DefaultHighlightBrushLazy.Value;

    public static readonly DependencyProperty HighlightBrushProperty =
        DependencyProperty.Register("HighlightBrush", typeof(Brush), typeof(FlowDocumentScrollViewerEx),
            new PropertyMetadata(DefaultHighlightBrush));

    public Brush HighlightBrush
    {
        get { return (Brush)GetValue(HighlightBrushProperty); }
        set { SetValue(HighlightBrushProperty, value); }
    }

    private HighlightAdorner? _highlightAdorner;

    private AdornerLayer? _adornerLayer;

    public FlowDocumentScrollViewerEx()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _adornerLayer = AdornerLayer.GetAdornerLayer(this);
        if (_adornerLayer != null && _highlightAdorner == null)
        {
            _highlightAdorner = new HighlightAdorner(this, HighlightBrush);
            _adornerLayer.Add(_highlightAdorner);

            // Initial application of highlights if properties were set before Loaded
            UpdateAdornerHighlights();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_highlightAdorner != null && _adornerLayer != null)
        {
            _adornerLayer.Remove(_highlightAdorner);
            // _highlightAdorner.Dispose(); // If Adorner implements IDisposable for LayoutUpdated unsubscription
            _highlightAdorner = null;
        }

        _adornerLayer = null;
        // Loaded -= OnLoaded; // Not strictly necessary if control is destroyed
        // Unloaded -= OnUnloaded;
    }

    public static readonly DependencyProperty HighlightableRangesProperty =
        DependencyProperty.Register(nameof(HighlightableRanges), typeof(IList<TextRange>),
            typeof(FlowDocumentScrollViewerEx),
            new PropertyMetadata(default(IList<TextRange>), OnHighlightableRangesChanged));

    public IList<TextRange>? HighlightableRanges
    {
        get { return (IList<TextRange>?)GetValue(HighlightableRangesProperty); }
        set { SetValue(HighlightableRangesProperty, value); }
    }

    private static void OnHighlightableRangesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((FlowDocumentScrollViewerEx)d).UpdateAdornerHighlights();
    }

    private void UpdateAdornerHighlights()
    {
        _highlightAdorner?.SetHighlights(HighlightableRanges);
    }

    public void ScrollToRange(TextRange range)
    {
        this.UpdateAdornerCurrentHighlightAndScroll(range);
    }

    private void UpdateAdornerCurrentHighlightAndScroll(TextRange range)
    {
        _highlightAdorner?.SetCurrentHighlight(range);
        if (!range.IsEmpty)
        {
            var startRect = range.Start.GetCharacterRect(LogicalDirection.Forward);
            if (!startRect.IsEmpty && startRect != Rect.Empty)
            {
                // BringIntoView(Rect) is generally preferred for FlowDocumentScrollViewer
                // as it gives more control over the visible region.
                this.BringIntoView(startRect);
            }
            else if (range.Start.Parent is FrameworkContentElement contentElement)
            {
                // Fallback if GetCharacterRect is empty or fails
                contentElement.BringIntoView();
            }
            // For very long highlights that span multiple screens, you might only see the beginning.
            // Further refinement could involve checking if the end of the range is also visible.
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        e.Handled = false;
    }
}