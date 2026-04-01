using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using LLMClient.Component.Render;

namespace LLMClient.Component.CustomControl;

/// <summary>
/// A lightweight WPF control that renders Markdown text inside a <see cref="TextBlock"/>.
/// <para>
/// Bind an <see cref="ObservableCollection{String}"/> to <see cref="Source"/>; the control
/// accumulates incoming chunks, parses the result with Markdig, and renders WPF
/// <see cref="Inline"/> elements via <see cref="MarkdownInlineRenderer"/>.
/// </para>
/// <para>
/// During streaming (rapid <see cref="NotifyCollectionChangedAction.Add"/> events) the
/// re-render is debounced to avoid excessive layout work. A full <see cref="Source"/>
/// replacement or <see cref="NotifyCollectionChangedAction.Reset"/> triggers an
/// immediate clear.
/// </para>
/// <para><b>Performance characteristics (8 000+ chars):</b></para>
/// <list type="bullet">
///   <item>Dirty tracking — skips re-render when content has not changed.</item>
///   <item>Adaptive debounce — interval scales from 80 ms (&lt;2 k chars) to 400 ms (&gt;10 k chars).</item>
///   <item>Streaming truncation — during streaming only the last
///         <see cref="MaxStreamingRenderLength"/> characters are parsed/rendered;
///         the full document is rendered by <see cref="StreamingRenderSession"/> in
///         the FlowDocument.</item>
/// </list>
/// </summary>
public class MarkdownTextBlock : Control
{
    // ─── Template parts ───────────────────────────────────────────────

    private TextBlock? _textBlock;
    private ScrollViewer? _scrollViewer;

    // ─── Rendering infrastructure ─────────────────────────────────────

    private readonly StringBuilder _accumulator = new(512);
    private readonly MarkdownInlineRenderer _renderer = new();
    private readonly DispatcherTimer _debounceTimer;

    /// <summary>Minimum debounce interval (small text, milliseconds).</summary>
    private const int DebounceMinMs = 80;

    /// <summary>
    /// During streaming, text beyond this length is truncated from the beginning
    /// before parsing. Set to 0 to disable truncation.
    /// The full content is always retained in <see cref="_accumulator"/> and will be
    /// rendered in full when <see cref="RebuildFromSource"/> is called.
    /// </summary>
    private const int MaxStreamingRenderLength = 4000;

    /// <summary>
    /// Tracks the <see cref="_accumulator"/> length at the time of the last successful
    /// render. Used to skip redundant re-renders when no new content has arrived.
    /// Reset to <c>-1</c> on <see cref="NotifyCollectionChangedAction.Reset"/> or
    /// source replacement.
    /// </summary>
    private int _lastRenderedLength = -1;

    // ─── Dependency properties ────────────────────────────────────────

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(ObservableCollection<string>), typeof(MarkdownTextBlock),
        new PropertyMetadata(default(ObservableCollection<string>), OnSourceChanged));

    public static readonly DependencyProperty TextWrappingProperty = DependencyProperty.Register(
        nameof(TextWrapping), typeof(TextWrapping), typeof(MarkdownTextBlock),
        new PropertyMetadata(TextWrapping.NoWrap));

    public ObservableCollection<string> Source
    {
        get => (ObservableCollection<string>)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public TextWrapping TextWrapping
    {
        get => (TextWrapping)GetValue(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    // ─── Constructor ──────────────────────────────────────────────────

    public MarkdownTextBlock()
    {
        Focusable = false;
        Template = BuildTemplate();

        _debounceTimer = new DispatcherTimer(DispatcherPriority.Render);
        _debounceTimer.Tick += OnDebounceTimerTick;
    }

    // ─── Template ─────────────────────────────────────────────────────

    private static ControlTemplate BuildTemplate()
    {
        var template = new ControlTemplate(typeof(MarkdownTextBlock));

        var scrollViewerFactory = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ScrollViewer");
        scrollViewerFactory.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        scrollViewerFactory.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        scrollViewerFactory.SetValue(ScrollViewer.FocusableProperty, false);
        scrollViewerFactory.SetValue(ScrollViewer.PaddingProperty, new Thickness(0));
        scrollViewerFactory.SetValue(ScrollViewer.BackgroundProperty, Brushes.Transparent);

        var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock), "PART_TextBlock");
        textBlockFactory.SetBinding(TextBlock.TextWrappingProperty,
            new Binding(nameof(TextWrapping)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        textBlockFactory.SetBinding(TextBlock.ForegroundProperty,
            new Binding(nameof(Foreground)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        textBlockFactory.SetBinding(TextBlock.FontSizeProperty,
            new Binding(nameof(FontSize)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        textBlockFactory.SetBinding(TextBlock.FontFamilyProperty,
            new Binding(nameof(FontFamily)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });

        scrollViewerFactory.AppendChild(textBlockFactory);
        template.VisualTree = scrollViewerFactory;
        return template;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _textBlock = GetTemplateChild("PART_TextBlock") as TextBlock;
        _scrollViewer = GetTemplateChild("PART_ScrollViewer") as ScrollViewer;
        RebuildFromSource();
    }

    // ─── Source property change ───────────────────────────────────────

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownTextBlock self)
            self.OnSourceCollectionReplaced(e);
    }

    private void OnSourceCollectionReplaced(DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ObservableCollection<string> oldCol)
            oldCol.CollectionChanged -= OnCollectionChanged;

        if (e.NewValue is ObservableCollection<string> newCol)
            newCol.CollectionChanged += OnCollectionChanged;

        RebuildFromSource();
    }

    // ─── Collection change handling ───────────────────────────────────

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems is not null)
                {
                    foreach (string item in e.NewItems)
                        _accumulator.Append(item);
                }
                RequestDebouncedRender();
                break;

            case NotifyCollectionChangedAction.Reset:
                _debounceTimer.Stop();
                _accumulator.Clear();
                _lastRenderedLength = -1;
                _textBlock?.Inlines.Clear();
                break;

            default:
                // Full rebuild for non-trivial mutations (Remove / Replace / Move)
                RebuildFromSource();
                break;
        }
    }

    // ─── Rendering ────────────────────────────────────────────────────

    /// <summary>
    /// Full rebuild: concatenate all items in Source, parse, and render immediately
    /// <b>without truncation</b>. Used when the entire Source collection is swapped,
    /// on first load, or for the History display.
    /// </summary>
    private void RebuildFromSource()
    {
        _debounceTimer.Stop();
        _accumulator.Clear();
        _lastRenderedLength = -1;

        if (Source is not null)
        {
            foreach (var item in Source)
                _accumulator.Append(item);
        }

        RenderNow(allowTruncation: false);
    }

    /// <summary>
    /// Start or restart the debounce timer. The interval scales with the current
    /// accumulator length to reduce CPU pressure on large documents.
    /// </summary>
    private void RequestDebouncedRender()
    {
        _debounceTimer.Stop();

        int ms = _accumulator.Length switch
        {
            < 2_000  => DebounceMinMs,   // 80 ms
            < 5_000  => 150,
            < 10_000 => 250,
            _        => 400,
        };

        _debounceTimer.Interval = TimeSpan.FromMilliseconds(ms);
        _debounceTimer.Start();
    }

    private void OnDebounceTimerTick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        RenderNow(allowTruncation: true);
    }

    /// <summary>
    /// Immediately parse the accumulated markdown and update the TextBlock inlines.
    /// </summary>
    /// <param name="allowTruncation">
    /// When <c>true</c> (streaming path) and the accumulated text exceeds
    /// <see cref="MaxStreamingRenderLength"/>, only the tail portion is parsed
    /// and rendered. The truncation point is aligned to the next newline to
    /// avoid breaking markdown syntax mid-line.
    /// </param>
    private void RenderNow(bool allowTruncation)
    {
        if (_textBlock is null) return;

        int len = _accumulator.Length;

        // ── Dirty check: skip if nothing changed since last render ────
        if (len == _lastRenderedLength) return;
        _lastRenderedLength = len;

        if (len == 0)
        {
            _textBlock.Inlines.Clear();
            return;
        }

        // ── Prepare text to render ────────────────────────────────────
        string text;
        if (allowTruncation && MaxStreamingRenderLength > 0 && len > MaxStreamingRenderLength)
        {
            // Truncate from the beginning, align to next newline
            int start = len - MaxStreamingRenderLength;
            for (int i = start; i < Math.Min(start + 200, len); i++)
            {
                if (_accumulator[i] == '\n') { start = i + 1; break; }
            }
            text = _accumulator.ToString(start, len - start);
        }
        else
        {
            text = _accumulator.ToString();
        }

        _renderer.Render(text, _textBlock.Inlines);
        _scrollViewer?.ScrollToEnd();
    }
}

