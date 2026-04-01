using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace LLMClient.Component.CustomControl;

public class MarkdownTextBlock : Control
{
    private TextBlock? _textBlock;
    private ScrollViewer? _scrollViewer;

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(ObservableCollection<string>), typeof(MarkdownTextBlock),
        new PropertyMetadata(default(ObservableCollection<string>), CollectionTextChangedCallback));

    public static readonly DependencyProperty TextWrappingProperty = DependencyProperty.Register(
        nameof(TextWrapping), typeof(TextWrapping), typeof(MarkdownTextBlock),
        new PropertyMetadata(TextWrapping.NoWrap));

    private static void CollectionTextChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownTextBlock textBlock)
        {
            textBlock.OnCollectionTextChanged(e);
        }
    }

    public ObservableCollection<string> Source
    {
        get { return (ObservableCollection<string>)GetValue(SourceProperty); }
        set { SetValue(SourceProperty, value); }
    }

    public TextWrapping TextWrapping
    {
        get { return (TextWrapping)GetValue(TextWrappingProperty); }
        set { SetValue(TextWrappingProperty, value); }
    }

    public MarkdownTextBlock()
    {
        Focusable = false;
        Template = BuildTemplate();
    }

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
        RebuildInlines();
    }

    private void OnCollectionTextChanged(DependencyPropertyChangedEventArgs args)
    {
        if (args.OldValue is ObservableCollection<string> oldCollection)
        {
            oldCollection.CollectionChanged -= OnCollectionChanged;
        }

        if (args.NewValue is ObservableCollection<string> newCollection)
        {
            newCollection.CollectionChanged += OnCollectionChanged;
        }

        RebuildInlines();
    }

    private void RebuildInlines()
    {
        if (_textBlock == null) return;

        var inlines = _textBlock.Inlines;
        inlines.Clear();

        isBold = false;
        isItalic = false;
        isCode = false;
        isCodeBlock = false;

        if (Source != null)
        {
            foreach (var str in Source)
            {
                inlines.Add(FromMarkdown(str));
            }
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_textBlock == null) return;

        _textBlock.BeginInit();
        var inlineCollection = _textBlock.Inlines;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                var enumerable = e.NewItems?.Cast<string>();
                if (enumerable != null)
                {
                    foreach (var se in enumerable)
                    {
                        inlineCollection.Add(FromMarkdown(se));
                    }
                }

                _scrollViewer?.ScrollToEnd();
                break;
            case NotifyCollectionChangedAction.Remove:
                break;
            case NotifyCollectionChangedAction.Replace:
                break;
            case NotifyCollectionChangedAction.Move:
                break;
            case NotifyCollectionChangedAction.Reset:
                inlineCollection.Clear();
                isBold = false;
                isItalic = false;
                isCode = false;
                isCodeBlock = false;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        _textBlock.EndInit();
    }

    bool isBold = false;

    bool isItalic = false;

    private bool isCode = false;

    private bool isCodeBlock = false;

    private Inline FromMarkdown(string raw)
    {
        if (raw == Environment.NewLine)
        {
            return new LineBreak();
        }

        switch (raw)
        {
            case "**":
                isBold = !isBold;
                break;
            case "*":
                isItalic = !isItalic;
                break;
            case "```":
                isCodeBlock = !isCodeBlock;
                break;
            case "`":
                isCode = !isCode;
                break;
        }

        var run = new Run(raw);

        if (isBold)
        {
            run.FontWeight = FontWeights.Bold;
        }

        if (isItalic)
        {
            run.FontStyle = FontStyles.Italic;
        }

        if (isCode)
        {
            run.Background = Brushes.LightGray;
        }


        return run;
    }
}