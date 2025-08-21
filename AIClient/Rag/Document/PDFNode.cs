using System.Windows;
using UglyToad.PdfPig.Outline.Destinations;

namespace LLMClient.Rag.Document;

/// <summary>
/// 表示PDF文档中的一个内容节点，可以对应一个目录章节。
/// </summary>
public class PDFNode
{
    /// <summary>
    /// 章节标题 (来自书签)
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// 在目录树中的层级
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// 该章节在PDF中的起始页码
    /// </summary>
    public int StartPage { get; set; }

    public Point StartPoint { get; set; }

    public ExplicitDestination? Destination { get; set; }

    /// <summary>
    /// 该章节的子节点 (子目录)
    /// </summary>
    public List<PDFNode> Children { get; set; } = new List<PDFNode>();

    public bool HasChildren => Children.Count > 0;

    /// <summary>
    /// 该节点直接包含的段落列表。只有叶子节点或需要存储自身内容的节点才填充。
    /// </summary>
    public List<PDFPage> Pages { get; set; } = new List<PDFPage>();

    public string Summary { get; set; } = string.Empty;

    public PDFNode(string title, int level)
    {
        Title = title;
        Level = level;
    }

    public override string ToString()
    {
        // 主要用于调试时方便查看
        return $"{new string(' ', Level * 2)}- {Title} (Page: {StartPage}, Pages: {Pages.Count})";
    }
}