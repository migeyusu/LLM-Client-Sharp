using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace LLMClient.UI.Component;

public class HighlightAdorner : Adorner
{
    private readonly List<HighlightInfo> _highlightInfos = new List<HighlightInfo>();
    private TextRange? _currentHighlightRange;
    private readonly Brush _highlightBrush;
    private readonly Pen _currentHighlightBorderPen; // Optional: for a border around current highlight

    // Helper class to store range and its calculated rectangles
    private class HighlightInfo
    {
        public TextRange Range { get; }
        public List<Rect> Rects { get; }

        public HighlightInfo(TextRange range, List<Rect> rects)
        {
            Range = range;
            Rects = rects;
        }
    }

    public HighlightAdorner(UIElement adornedElement, Brush highlightBrush) : base(adornedElement)
    {
        _highlightBrush = highlightBrush;
        // Optional: Define a pen for the border of the current highlight
        _currentHighlightBorderPen = new Pen(Brushes.DarkBlue, 1.5);
        IsHitTestVisible = false; // Adorner should not interfere with mouse events on underlying content
    }

    public void SetHighlights(IEnumerable<TextRange>? rangesToHighlight)
    {
        _highlightInfos.Clear();
        _currentHighlightRange = null;
        if (rangesToHighlight == null)
        {
            InvalidateVisual();
            return;
        }

        foreach (var range in rangesToHighlight)
        {
            if (!range.IsEmpty)
            {
                List<Rect> rects = CalculateRectsForRange(range);
                _highlightInfos.Add(new HighlightInfo(range, rects));
            }
        }

        InvalidateVisual(); // Trigger a re-render
    }

    public void SetCurrentHighlight(TextRange? currentRange)
    {
        _currentHighlightRange = currentRange;
        InvalidateVisual();
    }
    
private List<Rect> CalculateRectsForRange(TextRange range)
{
    var rects = new List<Rect>();
    if (range.IsEmpty) return rects;

    TextPointer navigator = range.Start;
    TextPointer endPointer = range.End;

    while (navigator != null && navigator.CompareTo(endPointer) < 0)
    {
        // 获取当前字符的开始位置矩形
        Rect startRect = navigator.GetCharacterRect(LogicalDirection.Forward);
        
        // 获取下一个字符位置
        TextPointer nextCharPointer = navigator.GetPositionAtOffset(1, LogicalDirection.Forward);
        
        // 如果到达了范围末尾，使用endPointer
        if (nextCharPointer == null || nextCharPointer.CompareTo(endPointer) > 0)
        {
            nextCharPointer = endPointer;
        }
        
        // 获取字符结束位置的矩形
        Rect endRect = nextCharPointer.GetCharacterRect(LogicalDirection.Backward);
        
        // 计算完整的字符矩形
        if (!startRect.IsEmpty && !endRect.IsEmpty)
        {
            Rect charRect = new Rect(
                startRect.Left,
                Math.Min(startRect.Top, endRect.Top),
                Math.Max(endRect.Right - startRect.Left, 0),
                Math.Max(startRect.Height, endRect.Height)
            );
            
            // 合并相邻矩形的逻辑保持不变
            if (rects.Count > 0)
            {
                Rect lastRect = rects[rects.Count - 1];
                const double yTolerance = 2.0;

                bool onSameLine = Math.Abs(lastRect.Top - charRect.Top) < yTolerance &&
                                  Math.Abs(lastRect.Bottom - charRect.Bottom) < yTolerance &&
                                  charRect.Left >= lastRect.Left - yTolerance;

                if (onSameLine)
                {
                    rects[rects.Count - 1] = Rect.Union(lastRect, charRect);
                }
                else
                {
                    rects.Add(charRect);
                }
            }
            else
            {
                rects.Add(charRect);
            }
        }
        
        // 移动到下一个字符
        if (nextCharPointer.CompareTo(endPointer) >= 0)
        {
            break;
        }
        
        navigator = nextCharPointer;
    }

    return rects;
}


    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        foreach (var info in _highlightInfos)
        {
            Pen? penToUse = null;
            if (_currentHighlightRange != null &&
                info.Range.Start.CompareTo(_currentHighlightRange.Start) == 0 &&
                info.Range.End.CompareTo(_currentHighlightRange.End) == 0)
            {
                penToUse = _currentHighlightBorderPen; // Apply border to current highlight
            }

            foreach (var rect in info.Rects)
            {
                if (!rect.IsEmpty)
                {
                    // Ensure rects are within the adorned element's bounds (optional, but good practice)
                    // This might be tricky if FlowDocumentScrollViewer is scrolled.
                    // GetCharacterRect usually gives coordinates relative to the FlowDocument content.
                    drawingContext.DrawRectangle(_highlightBrush, penToUse, rect);
                }
            }
        }
    }
}