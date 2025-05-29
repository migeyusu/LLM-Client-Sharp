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

    public void SetHighlights(IEnumerable<TextRange> rangesToHighlight)
    {
        _highlightInfos.Clear();
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

    public void ClearHighlights()
    {
        _highlightInfos.Clear();
        _currentHighlightRange = null;
        InvalidateVisual();
    }

    // This is the most complex part: calculating bounding rectangles for a TextRange
    // A TextRange can span multiple lines, so it might need multiple Rects.
    private List<Rect> CalculateRectsForRange(TextRange range)
    {
        var rects = new List<Rect>();
        if (range.IsEmpty) return rects;

        TextPointer navigator = range.Start;
        while (navigator.CompareTo(range.End) < 0)
        {
            // Get the rectangle for the character at the current navigator position
            Rect charRect = navigator.GetCharacterRect(LogicalDirection.Forward);

            // Skip empty rectangles (e.g., for line breaks or non-visible characters)
            if (charRect == Rect.Empty)
            {
                TextPointer nextContextPosition = navigator.GetNextContextPosition(LogicalDirection.Forward);
                if (nextContextPosition == null || nextContextPosition.CompareTo(range.End) >= 0) break;
                navigator = nextContextPosition;
                continue;
            }

            // Try to merge with the last rect if on the same visual line
            if (rects.Count > 0)
            {
                Rect lastRect = rects[rects.Count - 1];
                // Heuristic for "same line": similar Y coordinates and charRect is to the right
                // Tolerance for Y comparison can be useful due to subpixel rendering
                const double yTolerance = 2.0;
                if (Math.Abs(lastRect.Top - charRect.Top) < yTolerance &&
                    Math.Abs(lastRect.Bottom - charRect.Bottom) < yTolerance &&
                    charRect.Left >= lastRect.Left - yTolerance) // Allow slight overlap or adjacency
                {
                    rects[rects.Count - 1] = Rect.Union(lastRect, charRect);
                }
                else
                {
                    // If charRect is completely to the left of lastRect and on a new line (higher Top),
                    // it indicates a new line. Also, if Top is significantly different.
                    if (charRect.Top > lastRect.Bottom - yTolerance || charRect.Right < lastRect.Left)
                    {
                        rects.Add(charRect);
                    }
                    else // Should be part of the current line logic, but as a fallback
                    {
                        rects.Add(charRect);
                    }
                }
            }
            else
            {
                rects.Add(charRect);
            }

            // Move to the next character position within the range
            TextPointer nextCharPointer = navigator.GetPositionAtOffset(1, LogicalDirection.Forward);
            // Ensure we don't go past the end of the range or document
            if (nextCharPointer == null || nextCharPointer.CompareTo(range.End) > 0)
                break;
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