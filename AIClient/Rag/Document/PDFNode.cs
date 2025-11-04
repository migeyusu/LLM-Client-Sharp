using System.Windows;
using UglyToad.PdfPig.Outline.Destinations;

namespace LLMClient.Rag.Document;

/// <summary>
/// 表示PDF文档中的一个内容节点，可以对应一个目录章节。
/// </summary>
public class PDFNode : RawNode<PDFNode, PDFPage>
{
    private Point _startPoint;
    private double _startPointX;
    private double _startPointY;

    /// <summary>
    /// 该章节在PDF中的起始页码
    /// </summary>
    public int StartPage { get; set; }

    public Point StartPoint
    {
        get => _startPoint;
        set
        {
            if (value.Equals(_startPoint)) return;
            _startPoint = value;
            OnPropertyChanged();
            this.StartPointX = value.X;
            this.StartPointY = value.Y;
        }
    }

    public double StartPointX
    {
        get => _startPointX;
        set
        {
            if (value.Equals(_startPointX)) return;
            _startPointX = value;
            OnPropertyChanged();
        }
    }

    public double StartPointY
    {
        get => _startPointY;
        set
        {
            if (value.Equals(_startPointY)) return;
            _startPointY = value;
            OnPropertyChanged();
        }
    }

    public ExplicitDestination? Destination { get; set; }
    // public List<PDFNode> Children { get; set; } = new List<PDFNode>();

    /// <summary>
    /// 该节点直接包含的段落列表。只有叶子节点或需要存储自身内容的节点才填充。
    /// </summary>
    // public List<PDFPage> Pages { get; set; } = new List<PDFPage>();
    public PDFNode(string title, int level) : base(title)
    {
        Title = title;
        Level = level;
    }

    public override string ToString()
    {
        // 主要用于调试时方便查看
        return $"{new string(' ', Level * 2)}- {Title} (Page: {StartPage}, Pages: {ContentUnits.Count})";
    }
}