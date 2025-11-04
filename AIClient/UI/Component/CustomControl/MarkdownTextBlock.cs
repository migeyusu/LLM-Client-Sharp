using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace LLMClient.UI.Component.CustomControl;

public class MarkdownTextBlock : TextBlock
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(ObservableCollection<string>), typeof(MarkdownTextBlock),
        new PropertyMetadata(default(ObservableCollection<string>), CollectionTextChangedCallback));

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

    private void OnCollectionTextChanged(DependencyPropertyChangedEventArgs args)
    {
        if (args.OldValue is ObservableCollection<string> oldCollection)
        {
            oldCollection.CollectionChanged -= OnCollectionChanged;
        }

        var inlines = this.Inlines;
        inlines.Clear();
        if (args.NewValue is ObservableCollection<string> newCollection)
        {
            newCollection.CollectionChanged += OnCollectionChanged;
            foreach (var str in newCollection)
            {
                inlines.Add(FromMarkdown(str));
            }
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.BeginInit();
        var inlineCollection = this.Inlines;
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

                break;
            case NotifyCollectionChangedAction.Remove:
                break;
            case NotifyCollectionChangedAction.Replace:
                break;
            case NotifyCollectionChangedAction.Move:
                break;
            case NotifyCollectionChangedAction.Reset:
                inlineCollection.Clear();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        this.EndInit();
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

        /*if (isCodeBlock)
        {
            run.Background = Brushes.DarkGray;
        }*/

        return run;
    }
}