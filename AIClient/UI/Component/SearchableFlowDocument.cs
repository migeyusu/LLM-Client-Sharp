/*using System.Collections;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using LLMClient.Render;

namespace LLMClient.UI.Component;

public class SearchableFlowDocument
{
    private readonly List<Run> _highlightedRuns = new List<Run>();

    public IReadOnlyList<Run> HighlightedRuns => _highlightedRuns.AsReadOnly();
    public FlowDocument Document { get; set; }

    public bool HasHighlights => _highlightedRuns.Any();

    private int _currentHighlightIndex = -1;
    public int CurrentHighlightIndex => _currentHighlightIndex;

    public SearchableFlowDocument(FlowDocument document)
    {
        Document = document;
    }

    public void ApplySearch(string searchText, Brush highlightBrush)
    {
        if (string.IsNullOrEmpty(searchText))
            return;

        ApplySearch(Document, searchText, highlightBrush);
    }

    private void ApplySearch(FlowDocument document, string searchText, Brush highlightBrush)
    {
        // 1. 清除之前的高亮并尝试合并Run（重要）
        ClearAndNormalizeHighlights(document);

        if (string.IsNullOrEmpty(searchText))
            return;

        // 收集所有Run元素（包括TextmateColoredRun）
        var allRuns = new List<Run>();
        CollectRunsRecursive(document.Blocks, allRuns);

        // 对每个Run进行处理
        // 我们需要从后向前处理或者处理副本，因为修改Inlines集合会影响迭代
        for (var i = allRuns.Count - 1; i >= 0; i--)
        {
            var currentRun = allRuns[i];

            // Run可能已经被替换或移除（例如，在之前的迭代中被合并或分割）
            if (currentRun.Parent == null)
                continue;

            var runText = currentRun.Text;
            if (string.IsNullOrEmpty(runText))
                continue;

            IList? ownerInlines = GetOwnerInlines(currentRun);
            if (ownerInlines == null)
                continue;

            var searchStartIndex = 0;
            var replacementInlines = new List<Inline>();
            var lastMatchEnd = 0; // 记录上一个匹配的结束位置在runText中的索引

            while (searchStartIndex < runText.Length)
            {
                var matchIndexInRun = runText.IndexOf(searchText, searchStartIndex, StringComparison.OrdinalIgnoreCase);
                if (matchIndexInRun != -1)
                {
                    // 1. 添加匹配前的部分 (Prefix)
                    if (matchIndexInRun > lastMatchEnd)
                    {
                        var prefixText = runText.Substring(lastMatchEnd, matchIndexInRun - lastMatchEnd);
                        var cloneRunInstance = CloneRunInstance(currentRun, prefixText);
                        if (cloneRunInstance != null)
                        {
                            replacementInlines.Add(cloneRunInstance);
                        }
                    }

                    // 2. 添加高亮部分 (Matched Text) - 使用标准Run
                    var matchedText = runText.Substring(matchIndexInRun, searchText.Length);
                    var highlightedRun = new Run(matchedText) { Background = highlightBrush };
                    CopyRunFormatting(currentRun, highlightedRun); // 保持原始Run的字体等格式
                    replacementInlines.Add(highlightedRun);

                    lastMatchEnd = matchIndexInRun + searchText.Length;
                    searchStartIndex = lastMatchEnd;
                }
                else
                {
                    // 没有更多匹配了，跳出循环
                    break;
                }
            }

            // 3. 添加最后一个匹配后的部分 (Suffix)
            if (lastMatchEnd < runText.Length)
            {
                var suffixText = runText.Substring(lastMatchEnd);
                var cloneRunInstance = CloneRunInstance(currentRun, suffixText);
                if (cloneRunInstance != null)
                {
                    replacementInlines.Add(cloneRunInstance);
                }
            }

            // 如果有任何替换发生 (即至少有一个匹配)
            if (replacementInlines.Any())
            {
                var originalRunIndex = ownerInlines.IndexOf(currentRun);
                if (originalRunIndex != -1)
                {
                    ownerInlines.RemoveAt(originalRunIndex);
                    for (var j = replacementInlines.Count - 1; j >= 0; j--)
                    {
                        var inlineToInsert = replacementInlines[j];
                        ownerInlines.Insert(originalRunIndex, inlineToInsert);
                        // If this inline is one of our highlighted runs, add it
                        if (inlineToInsert is Run r && r.Background == highlightBrush)
                        {
                            // Add to the beginning to keep order somewhat similar to appearance
                            _highlightedRuns.Insert(0, r);
                        }
                    }
                }
            }
        }

        _currentHighlightIndex = _highlightedRuns.Any() ? 0 : -1; // 搜索后默认指向第一个
  }

    public Run? GetHighlight(int index)
    {
        if (index >= 0 && index < _highlightedRuns.Count)
        {
            return _highlightedRuns[index];
        }

        return null;
    }

    public Run? GetCurrentHighlight()
    {
        return GetHighlight(_currentHighlightIndex);
    }

    public Run? MoveToNextHighlight()
    {
        if (!_highlightedRuns.Any()) return null;
        _currentHighlightIndex++;
        if (_currentHighlightIndex >= _highlightedRuns.Count)
        {
            _currentHighlightIndex = 0; // 循环到开头
        }

        return _highlightedRuns[_currentHighlightIndex];
    }

    public Run? MoveToPreviousHighlight()
    {
        if (!_highlightedRuns.Any()) return null;
        _currentHighlightIndex--;
        if (_currentHighlightIndex < 0)
        {
            _currentHighlightIndex = _highlightedRuns.Count - 1; // 循环到末尾
        }

        return _highlightedRuns[_currentHighlightIndex];
    }

    public void SetCurrentHighlightIndex(int index)
    {
        if (index >= -1 && index < _highlightedRuns.Count)
        {
            _currentHighlightIndex = index;
        }
        else if (_highlightedRuns.Any())
        {
            _currentHighlightIndex = 0; // 默认或无效索引
        }
        else
        {
            _currentHighlightIndex = -1;
        }
    }

    private static InlineCollection? GetOwnerInlines(Inline inline)
    {
        if (inline.Parent is Paragraph p) return p.Inlines;
        if (inline.Parent is Span s) return s.Inlines;
        // 可以根据需要添加对其他容器（如 ListItem）的支持
        return null;
    }

    private static Run? CloneRunInstance(Run originalRun, string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        Run newRun;
        if (originalRun is TextmateColoredRun tmRun) // 替换为你的实际类型
        {
            // 假设 TextmateColoredRun 有一个 (string, IToken) 构造函数
            // 并且 Token 属性是可访问的
            newRun = new TextmateColoredRun(text, tmRun.Token);
        }
        else
        {
            newRun = new Run(text);
        }

        CopyRunFormatting(originalRun, newRun);
        return newRun;
    }

    private static void CopyRunFormatting(Run source, Run target)
    {
        target.Foreground = source.Foreground;
        target.FontFamily = source.FontFamily;
        target.FontSize = source.FontSize;
        target.FontWeight = source.FontWeight;
        target.FontStyle = source.FontStyle;
        target.TextDecorations = source.TextDecorations?.CloneCurrentValue(); // TextDecorations 是 Freezable
        // 复制其他你关心的格式属性
        var originalStyleValue = source.ReadLocalValue(FrameworkElement.StyleProperty);
        if (originalStyleValue != DependencyProperty.UnsetValue && originalStyleValue is Style style)
        {
            // 如果原始 Run 有一个本地设置的 Style (例如，由 TextMateCodeRenderer 设置的)，则复制它。
            // 这个 Style 对象本身就包含了 Setter 和 DynamicResource 引用。
            target.Style = style;
        }
    }

    private void ClearAndNormalizeHighlights(FlowDocument document)
    {
        _highlightedRuns.Clear();
        _currentHighlightIndex = -1;
        var allRuns = new List<Run>();
        var blockCollection = document.Blocks;
        CollectRunsRecursive(blockCollection, allRuns);
        foreach (var run in allRuns)
        {
            // 清除背景高亮
            if (run.ReadLocalValue(TextElement.BackgroundProperty) != DependencyProperty.UnsetValue)
            {
                run.ClearValue(TextElement.BackgroundProperty);
            }
        }

        // 标准化：合并相邻的兼容 Run
        // 这是一个重要步骤，防止Run数量无限增长
        // 从后向前遍历，方便合并时操作集合
        for (var i = blockCollection.Count - 1; i >= 0; i--)
        {
            NormalizeInlinesInBlock(blockCollection.ElementAt(i));
        }
    }

    private static void NormalizeInlinesInBlock(Block block)
    {
        if (block is Paragraph paragraph)
        {
            NormalizeInlineCollection(paragraph.Inlines);
        }
        else if (block is Section section)
        {
            for (var i = section.Blocks.Count - 1; i >= 0; i--)
            {
                NormalizeInlinesInBlock(section.Blocks.ElementAt(i));
            }
        }
        // 可以添加对 List, Table 等的处理
    }

    private static void NormalizeInlineCollection(InlineCollection inlines)
    {
        if (inlines.Count < 2) return;

        for (var i = inlines.Count - 1; i > 0; i--)
        {
            var currentInline = inlines.ElementAt(i);
            var previousInline = inlines.ElementAt(i - 1);

            if (CanMergeRuns(previousInline as Run, currentInline as Run))
            {
                var run1 = (Run)previousInline;
                var run2 = (Run)currentInline;

                var combinedText = run1.Text + run2.Text;
                var mergedRun = CloneRunInstance(run1, combinedText); // 使用run1作为模板

                // 从集合中移除旧的runs，并插入新的合并后的run
                // 确保索引正确
                inlines.Remove(run1);
                inlines.Remove(run2);
                ((IList)inlines).Insert(i - 1, mergedRun);
                // 由于移除了两个并插入一个，下一个迭代的 i 需要调整，但从后向前迭代时，
                // i-- 自然会处理前一个元素，这里不需要特殊调整 i。
            }
            else if (currentInline is Span currentSpan)
            {
                NormalizeInlineCollection(currentSpan.Inlines);
            }

            // 如果 previousInline 是 Span，也可能需要递归处理
            if (previousInline is Span prevSpan && i == 1) // 特殊处理第一个元素如果是Span的情况
            {
                NormalizeInlineCollection(prevSpan.Inlines);
            }
        }

        // 如果第一个元素是 Span (在循环结束后)
        if (inlines.FirstInline is Span firstSpan)
        {
            NormalizeInlineCollection(firstSpan.Inlines);
        }
    }

    private static bool CanMergeRuns(Run? run1, Run? run2)
    {
        if (run1 == null || run2 == null) return false;

        // 不能合并，如果它们有不同的背景（意味着其中一个可能是我们主动高亮的）
        if (run1.ReadLocalValue(TextElement.BackgroundProperty) != run2.ReadLocalValue(TextElement.BackgroundProperty))
            return false;
        if (run1.ReadLocalValue(TextElement.BackgroundProperty) != DependencyProperty.UnsetValue) // 如果有背景，不合并
            return false;


        // 检查类型是否兼容
        if (run1.GetType() != run2.GetType()) return false;

        if (run1 is TextmateColoredRun tmRun1 &&
            run2 is TextmateColoredRun tmRun2)
        {
            // 对于 TextmateColoredRun，还需要检查 Token 是否等效
            // 假设 IToken 有某种方式可以比较（例如，ScopeName 或内容）
            // if (!AreTokensEquivalent(tmRun1.Token, tmRun2.Token)) return false;
            // 简单起见，这里假设如果Token对象引用相同或内容相同，则可合并
            if (!Equals(tmRun1.Token, tmRun2.Token)) // 你可能需要更复杂的Token比较逻辑
            {
                // 你可能需要更细致的比较，例如 Token 的关键属性
                // if (tmRun1.Token?.ScopeName != tmRun2.Token?.ScopeName) return false;
                return false; // 简化：如果Token实例不同就不合并
            }

            // 检查 ThemeColors 是否相同
            var theme1 = tmRun1.GetValue(TextmateColoredRun.ThemeColorsProperty);
            var theme2 = tmRun2.GetValue(TextmateColoredRun.ThemeColorsProperty);
            if (!Equals(theme1, theme2)) return false;
        }

        // 检查基本格式属性是否相同
        return run1.Foreground == run2.Foreground &&
               Equals(run1.FontFamily, run2.FontFamily) &&
               Math.Abs(run1.FontSize - run2.FontSize) < double.Epsilon &&
               run1.FontWeight == run2.FontWeight &&
               run1.FontStyle == run2.FontStyle &&
               TextDecorationsEqual(run1.TextDecorations, run2.TextDecorations);
    }

    private static bool TextDecorationsEqual(TextDecorationCollection? tdc1, TextDecorationCollection? tdc2)
    {
        if (tdc1 == null && tdc2 == null) return true;
        if (tdc1 == null || tdc2 == null) return false;
        if (tdc1.Count != tdc2.Count) return false;
        // 这个比较可能需要更深入，但对于常用情况可能足够
        return !tdc1.Except(tdc2).Any() && !tdc2.Except(tdc1).Any();
    }

    private static void CollectRunsRecursive(FrameworkContentElement parent, List<Run> runs)
    {
        if (parent is Run run)
        {
            runs.Add(run);
        }
        else if (parent is Paragraph paragraph)
        {
            foreach (var inline in paragraph.Inlines) CollectRunsRecursive(inline, runs);
        }
        else if (parent is Span span)
        {
            foreach (var inline in span.Inlines) CollectRunsRecursive(inline, runs);
        }
        else if (parent is Section section)
        {
            foreach (var block in section.Blocks) CollectRunsRecursive(block, runs);
        }
        else if (parent is List list)
        {
            foreach (var listItem in list.ListItems) CollectRunsRecursive(listItem, runs);
        }
        else if (parent is ListItem listItem)
        {
            foreach (var block in listItem.Blocks) CollectRunsRecursive(block, runs);
        }
        else if (parent is Table table)
        {
            foreach (var group in table.RowGroups)
            {
                foreach (var row in group.Rows)
                {
                    foreach (var cell in row.Cells)
                    {
                        foreach (var block in cell.Blocks) CollectRunsRecursive(block, runs);
                    }
                }
            }
        }
        // 你可能需要扩展这个以覆盖所有可能的 FlowDocument 元素类型
    }

    private static void CollectRunsRecursive(BlockCollection blocks, List<Run> runs)
    {
        foreach (var block in blocks)
        {
            CollectRunsRecursive(block, runs);
        }
    }
}*/


using System.Windows.Documents;
using LLMClient.UI;

public class SearchableFlowDocument : BaseViewModel
{
    private readonly List<TextRange> _foundTextRanges = new List<TextRange>();
    public FlowDocument Document { get; }

    public IReadOnlyList<TextRange> FoundTextRanges => _foundTextRanges.AsReadOnly();

    public bool HasMatched => _foundTextRanges.Any();

    public SearchableFlowDocument(FlowDocument document)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /*public void ApplySearch(string? searchText)
    {
        _foundTextRanges.Clear();
        if (string.IsNullOrEmpty(searchText))
        {
            OnPropertyChanged(nameof(FoundTextRanges));
            return;
        }

        TextPointer currentPosition = Document.ContentStart;
        while (currentPosition != null && currentPosition.CompareTo(Document.ContentEnd) < 0)
        {
            // Optimized search to avoid creating TextRange for every char
            TextPointer nextContextPosition = currentPosition.GetNextContextPosition(LogicalDirection.Forward);
            if (nextContextPosition == null)
            {
                nextContextPosition = Document.ContentEnd;
            }

            // Search within the text of the current segment
            // TextRange.Text is expensive if called repeatedly in a loop.
            // Alternative: Iterate with GetTextInRun or GetTextElementEnumerator
            // For simplicity, using TextRange here, but be mindful of performance on very large docs.
            var segmentRange = new TextRange(currentPosition, nextContextPosition);
            string segmentText = segmentRange.Text;

            int indexInSegment = 0;
            while (indexInSegment < segmentText.Length)
            {
                int matchPos = segmentText.IndexOf(searchText, indexInSegment, StringComparison.OrdinalIgnoreCase);
                if (matchPos != -1)
                {
                    TextPointer? matchStart = currentPosition.GetPositionAtOffset(matchPos);
                    TextPointer? matchEnd = matchStart?.GetPositionAtOffset(searchText.Length);

                    if (matchStart != null && matchEnd != null)
                    {
                        _foundTextRanges.Add(new TextRange(matchStart, matchEnd));
                    }

                    indexInSegment = matchPos + searchText.Length;
                }
                else
                {
                    break; // No more matches in this segment
                }
            }

            currentPosition = nextContextPosition;
        }

        OnPropertyChanged(nameof(FoundTextRanges));
    }*/
    
    public void ApplySearch(string? searchText)
{
    _foundTextRanges.Clear();
    if (string.IsNullOrEmpty(searchText))
    {
        OnPropertyChanged(nameof(FoundTextRanges));
        return;
    }

    // 使用TextPointer遍历整个文档
    FindTextInDocument(Document, searchText);
    OnPropertyChanged(nameof(FoundTextRanges));
}

private void FindTextInDocument(FlowDocument document, string searchText)
{
    TextPointer position = document.ContentStart;
    
    while (position != null && position.CompareTo(document.ContentEnd) < 0)
    {
        // 查找下一个文本运行
        if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
        {
            string textRun = position.GetTextInRun(LogicalDirection.Forward);
            
            // 在当前文本运行中查找所有匹配
            int indexInRun = 0;
            while (indexInRun < textRun.Length)
            {
                int index = textRun.IndexOf(searchText, indexInRun, StringComparison.OrdinalIgnoreCase);
                if (index != -1)
                {
                    // 创建精确的TextPointer
                    TextPointer start = position.GetPositionAtOffset(index, LogicalDirection.Forward);
                    TextPointer end = start?.GetPositionAtOffset(searchText.Length, LogicalDirection.Forward);
                    
                    if (start != null && end != null)
                    {
                        // 验证找到的文本是否真的匹配
                        var foundRange = new TextRange(start, end);
                        if (string.Equals(foundRange.Text, searchText, StringComparison.OrdinalIgnoreCase))
                        {
                            _foundTextRanges.Add(foundRange);
                        }
                    }
                    
                    indexInRun = index + searchText.Length;
                }
                else
                {
                    break;
                }
            }
        }
        
        // 移动到下一个位置
        position = position.GetNextContextPosition(LogicalDirection.Forward);
    }
}
}