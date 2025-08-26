using Microsoft.Extensions.Logging;

namespace LLMClient.Rag.Document;

/// <summary>
/// raw node for representing a hierarchical structure in a document.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <typeparam name="TK"></typeparam>
public abstract class RawNode<T, TK> where T : RawNode<T, TK>
    where TK : IContentUnit
{
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

    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// 该章节的子节点 (子目录)
    /// </summary>
    public List<T> Children { get; set; } = new List<T>();

    public bool HasChildren => Children.Count > 0;

    public List<TK> ContentUnits { get; set; } = new List<TK>();
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