using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using LLMClient.Component.ViewModel.Base;
using Microsoft.Extensions.Logging;

namespace LLMClient.Rag.Document;

public interface IRawNode
{
    string Title { get; set; }

    int Level { get; set; }

    bool HasChildren { get; }

    /// <summary>
    /// used for summary generation, may contain markdown or plain text.
    /// </summary>
    string GetSummaryRaw();
}

/// <summary>
/// raw node for representing a hierarchical structure in a document.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <typeparam name="TK"></typeparam>
public abstract class RawNode<T, TK> : BaseViewModel, IRawNode where T : RawNode<T, TK>
    where TK : IContentUnit
{
    private string _summary = string.Empty;

    protected RawNode(string title)
    {
        Title = title;
    }

    /// <summary>
    /// 章节标题 (来自书签)
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// 在目录树中的层级，从0开始
    /// </summary>
    public int Level { get; set; }

    public string Summary
    {
        get => _summary;
        set
        {
            if (value == _summary) return;
            _summary = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 该章节的子节点 (子目录)
    /// </summary>
    public List<T> Children { get; set; } = new List<T>();

    public bool HasChildren => Children.Count > 0;

    public string GetSummaryRaw()
    {
        if (HasChildren)
        {
            var summaryBuilder = new StringBuilder(this.Title + "\nSubsections:");
            foreach (var childNode in this.Children)
            {
                summaryBuilder.AppendLine($"- {childNode.Title}");
                summaryBuilder.AppendLine(childNode.Summary);
            }

            return summaryBuilder.ToString();
        }

        var nodeContentBuilder = new StringBuilder();
        var units = this.ContentUnits;
        if (units.Count > 0)
        {
            for (var index = 0; index < units.Count; index++)
            {
                var page = units[index];
                //note: paragraph不执行summary
                var pageContent = page.Content;
                if (string.IsNullOrEmpty(pageContent.Trim()))
                {
                    Trace.TraceWarning("跳过空页，所在节点：{0}，节点索引：{1}", this.Title, index);
                    continue; // 跳过空段落
                }

                nodeContentBuilder.AppendLine(pageContent);
            }
        }

        return nodeContentBuilder.ToString();
    }

    public ObservableCollection<TK> ContentUnits { get; set; } = new ObservableCollection<TK>();
}

public interface IContentUnit
{
    string Content { get; }

    /// <summary>
    /// base64 encoded images in the content unit.
    /// <para>format: base64:image/png;base64,iVBORw0KGgoAAAANSUhEUgAA...
    /// </para>
    /// </summary>
    Task<IList<string>> GetImages(ILogger? logger);
}