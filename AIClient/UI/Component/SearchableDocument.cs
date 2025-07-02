using System.Windows.Documents;
using LLMClient.Render;

namespace LLMClient.UI.Component;

public class SearchableDocument : BaseViewModel
{
    public string RawText { get; }

    private readonly List<TextRange> _foundTextRanges = new List<TextRange>();

    private string? _cachedSearchText = null;

    private bool _isSearchApplied = true;

    public FlowDocument Document { get; }

    public IReadOnlyList<TextRange> FoundTextRanges => _foundTextRanges.AsReadOnly();

    public bool HasMatched { get; private set; }

    public SearchableDocument(string rawText)
    {
        if (string.IsNullOrEmpty(rawText))
        {
            throw new ArgumentException("Raw text cannot be null or empty.", nameof(rawText));
        }

        RawText = rawText;
        Document = rawText.ToFlowDocument();
    }

    /// <summary>
    /// lazy search
    /// </summary>
    /// <param name="searchText"></param>
    public void ApplySearch(string? searchText)
    {
        _cachedSearchText = searchText;
        HasMatched = !string.IsNullOrEmpty(searchText) &&
                     RawText.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        _isSearchApplied = !HasMatched;
        if (_foundTextRanges.Count > 0)
        {
            _foundTextRanges.Clear();
            OnPropertyChanged(nameof(FoundTextRanges));
        }
    }

    public void EnsureSearch()
    {
        if (_isSearchApplied)
        {
            return;
        }

        if (!HasMatched || string.IsNullOrEmpty(_cachedSearchText))
        {
            return;
        }

        // 使用TextPointer遍历整个文档
        FindTextInDocument(Document, _cachedSearchText);
        _isSearchApplied = true;
        OnPropertyChanged(nameof(FoundTextRanges));
    }

    private void FindTextInDocument(FlowDocument document, string searchText)
    {
        var position = document.ContentStart;
        while (position != null && position.CompareTo(document.ContentEnd) < 0)
        {
            // 查找下一个文本运行
            if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                var textRun = position.GetTextInRun(LogicalDirection.Forward);
                // 在当前文本运行中查找所有匹配
                var indexInRun = 0;
                while (indexInRun < textRun.Length)
                {
                    var index = textRun.IndexOf(searchText, indexInRun, StringComparison.OrdinalIgnoreCase);
                    if (index != -1)
                    {
                        // 创建精确的TextPointer
                        var start = position.GetPositionAtOffset(index, LogicalDirection.Forward);
                        var end = start?.GetPositionAtOffset(searchText.Length, LogicalDirection.Forward);

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