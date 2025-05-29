using System.Diagnostics;
using System.Runtime.CompilerServices;
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

    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register("SearchText", typeof(string), typeof(FlowDocumentScrollViewerEx),
            new PropertyMetadata(null, OnSearchTextChanged));

    public string SearchText
    {
        get { return (string)GetValue(SearchTextProperty); }
        set { SetValue(SearchTextProperty, value); }
    }

    public static readonly DependencyProperty HighlightBrushProperty =
        DependencyProperty.Register("HighlightBrush", typeof(Brush), typeof(FlowDocumentScrollViewerEx),
            new PropertyMetadata(Brushes.Yellow));

    public Brush HighlightBrush
    {
        get { return (Brush)GetValue(HighlightBrushProperty); }
        set { SetValue(HighlightBrushProperty, value); }
    }
    
    public static readonly DependencyProperty BringToRunProperty = DependencyProperty.Register(
        nameof(BringToRun), typeof(Run), typeof(FlowDocumentScrollViewerEx), new PropertyMetadata(default(Run),
            new PropertyChangedCallback(OnBringToRunChanged)));

    private static void OnBringToRunChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FlowDocumentScrollViewerEx viewer && e.NewValue is Run runToView)
        {
            // 调用 BringRunIntoView 方法
            viewer.BringRunIntoView(runToView);
        }
    }

    public Run BringToRun
    {
        get { return (Run)GetValue(BringToRunProperty); }
        set { SetValue(BringToRunProperty, value); }
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
            UpdateAdornerCurrentHighlightAndScroll(false); // Don't scroll on initial load unless specified
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
        DependencyProperty.Register(nameof(HighlightableRanges), typeof(IEnumerable<TextRange>), 
            typeof(FlowDocumentScrollViewerEx),
            new PropertyMetadata(null, OnHighlightableRangesChanged));

    public IEnumerable<TextRange>? HighlightableRanges
    {
        get { return (IEnumerable<TextRange>?)GetValue(HighlightableRangesProperty); }
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

    public static readonly DependencyProperty CurrentHighlightRangeProperty =
        DependencyProperty.Register(nameof(CurrentHighlightRange), typeof(TextRange), 
            typeof(FlowDocumentScrollViewerEx),
            new PropertyMetadata(null, OnCurrentHighlightRangeChanged));

    public TextRange? CurrentHighlightRange
    {
        get { return (TextRange?)GetValue(CurrentHighlightRangeProperty); }
        set { SetValue(CurrentHighlightRangeProperty, value); }
    }

    private static void OnCurrentHighlightRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((FlowDocumentScrollViewerEx)d).UpdateAdornerCurrentHighlightAndScroll(true); // Scroll when current highlight changes
    }
    
    private void UpdateAdornerCurrentHighlightAndScroll(bool scrollToHighlight)
    {
        _highlightAdorner?.SetCurrentHighlight(CurrentHighlightRange);
        if (scrollToHighlight && CurrentHighlightRange != null && !CurrentHighlightRange.IsEmpty)
        {
            ScrollToRange(CurrentHighlightRange);
        }
    }
    
    private void ScrollToRange(TextRange range)
    {
        if (range == null || range.IsEmpty) return;

        Rect startRect = range.Start.GetCharacterRect(LogicalDirection.Forward);
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

    /// <summary>
    /// 将指定的 Run 元素滚动到视图中。
    /// </summary>
    /// <param name="runToView">要滚动到视图中的 Run 元素。</param>
    /// <returns>如果成功（或尝试过）滚动，则为 true。</returns>
    public bool BringRunIntoView(Run runToView)
    {
        if (runToView.Parent == null || this.Document == null)
        {
            return false;
        }

        // 验证 Run 是否属于当前 Document (可选但更健壮)
        // 这可以通过向上遍历 runToView.Parent 直到 FlowDocument，然后比较引用。
        // 或者简单地假设调用者传递了正确的 Run。

        // 找到 Run 所在的 Block
        DependencyObject? current = runToView;
        Block? containingBlock = null;
        while (current != null)
        {
            if (current is Block block)
            {
                containingBlock = block;
                break;
            }

            if (current is FrameworkContentElement fce)
            {
                current = fce.Parent;
            }
            else if (current is FrameworkElement fe) // 以防万一 Run 被包装在 UIElement 中
            {
                current = fe.Parent ?? VisualTreeHelper.GetParent(fe) ?? LogicalTreeHelper.GetParent(fe);
            }
            else
            {
                break; // 到达树顶或非预期类型
            }
        }

        if (containingBlock != null)
        {
            // 先确保 Block 可见
            containingBlock.BringIntoView();
            // 然后微调，确保 Run 本身也尽可能好地定位
            // 在某些情况下，Block.BringIntoView() 后，Run 可能仍未完全理想定位，
            // 再次调用 Run.BringIntoView() 可能有助于改善。
            Application.Current.Dispatcher.InvokeAsync(() => { runToView.BringIntoView(); },
                System.Windows.Threading.DispatcherPriority.Background); // 延迟一点可能效果更好

            return true;
        }
        else
        {
            // 如果找不到包含的Block，直接尝试滚动 Run
            runToView.BringIntoView();
            return true;
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        e.Handled = false;
    }
}