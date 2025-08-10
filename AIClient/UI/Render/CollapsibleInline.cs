using System.Windows;
using System.Windows.Documents;

namespace LLMClient.UI.Render;

public class CollapsibleInline : Span
{
    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(CollapsibleInline),
            new PropertyMetadata(false, OnIsExpandedChanged));

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    private readonly Run _headerRun;
    private readonly Span _contentSpan;
    private readonly Run _toggleRun;
   
    public CollapsibleInline(string header, string content)
    {
        _toggleRun = new Run("▶ ") { FontFamily = new System.Windows.Media.FontFamily("Segoe UI Symbol") };
        _headerRun = new Run(header) { FontWeight = FontWeights.Bold };
        _contentSpan = new Span();
        _contentSpan.Inlines.Add(new LineBreak());
        _contentSpan.Inlines.Add(new Run(content));
        
        Inlines.Add(_toggleRun);
        Inlines.Add(_headerRun);
        
        UpdateVisibility();
        
        // 添加点击事件
        MouseDown += (s, e) => { IsExpanded = !IsExpanded; e.Handled = true; };
        Cursor = System.Windows.Input.Cursors.Hand;
    }

    private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CollapsibleInline inline)
        {
            inline.UpdateVisibility();
        }
    }

    private void UpdateVisibility()
    {
        _toggleRun.Text = IsExpanded ? "▼ " : "▶ ";
        
        if (IsExpanded)
        {
            if (!Inlines.Contains(_contentSpan))
                Inlines.Add(_contentSpan);
        }
        else
        {
            if (Inlines.Contains(_contentSpan))
                Inlines.Remove(_contentSpan);
        }
    }
}