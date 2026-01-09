using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using LLMClient.Data;

namespace LLMClient.Component.CustomControl;

public class FlowDocumentScrollViewerEx : FlowDocumentScrollViewer
{
    public static readonly DependencyProperty SafeDocumentProperty = DependencyProperty.Register(
        nameof(SafeDocument), typeof(FlowDocument), typeof(FlowDocumentScrollViewerEx),
        new PropertyMetadata(default(FlowDocument), OnSafeDocumentChanged));

    private static void OnSafeDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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

    /// <summary>
    /// 绑定安全的 FlowDocument，自动处理父级关系
    /// </summary>
    public FlowDocument SafeDocument
    {
        get { return (FlowDocument)GetValue(SafeDocumentProperty); }
        set { SetValue(SafeDocumentProperty, value); }
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

    /// <summary>
    /// 搜索高亮颜色
    /// </summary>
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
        this.CommandBindings.Add(new CommandBinding(Markdig.Wpf.Commands.Image, async (s, e) =>
        {
            //在弹窗中显示图片，传入的是一个url
            if (e.Parameter is not string url)
            {
                return;
            }

            ImageSource? imageSource = null;
            try
            {
                if (url.IsBase64Image())
                {
                    imageSource = ImageExtensions.GetImageSourceFromBase64(url);
                }
                else
                {
                    if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
                    {
                        imageSource = await uri.GetImageSourceAsync();
                    }
                }

                var window = new Window()
                {
                    Title = "Image Preview",
                    Width = 800,
                    Height = 600,
                    Content = new ScrollViewer
                    {
                        Content = new Image
                        {
                            Source = imageSource,
                            Stretch = Stretch.Uniform
                        }
                    }
                };
                window.Show();
            }
            catch (Exception exception)
            {
                Trace.TraceWarning($"Failed to load image from URL '{url}': {exception.Message}");
            }

            e.Handled = true;
        }));
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