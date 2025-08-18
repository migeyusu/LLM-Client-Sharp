using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Data;

namespace LLMClient.Rag;

public class ChunkNode
{
    public ChunkNode(DocChunk chunk)
    {
        Chunk = chunk;
    }

    public DocChunk Chunk { get; }

    public ChunkNode? Parent { get; set; }

    public List<ChunkNode> Children { get; set; } = new List<ChunkNode>();

    public ChunkNode Root
    {
        get
        {
            var node = this;
            while (node.Parent != null)
            {
                node = node.Parent;
            }

            return node;
        }
    }

    public void AddChild(ChunkNode childNode)
    {
        childNode.Parent = this;
        this.Children.Add(childNode);
    }

    public string GetStructure(int level = 0)
    {
        if (this.Chunk.Type == (int)ChunkType.Paragraph)
        {
            return string.Empty;
        }

        var indent = new string(' ', level * 2);
        var result = $"{indent}- {Chunk.Title}\r\n";
        if (Chunk.Summary.Length > 0)
            result += $"{indent}  Summary: {Chunk.Summary}\r\n";
        foreach (var child in Children)
        {
            result += child.GetStructure(level + 1);
        }

        return result;
    }

    public string GetView(int level = 0)
    {
        var indent = new string(' ', level * 2);
        var result = string.Empty;
        if (Chunk.Type == (int)ChunkType.Bookmark)
        {
            result += $"{indent}- {Chunk.Title}\r\n";
            foreach (var child in Children)
            {
                result += child.GetView(level + 1);
            }
        }
        else if (Chunk.Type == (int)ChunkType.Paragraph)
        {
            var chunkText = Chunk.Text;
            if (!string.IsNullOrEmpty(chunkText))
            {
                result += $"{indent}{chunkText}\r\n";
            }
        }

        return result;
    }
}

public class DocChunk
{
    [VectorStoreKey] public string Key { get; set; } = string.Empty;

    /// <summary>
    /// raw data
    /// </summary>
    [VectorStoreData(IsFullTextIndexed = true)]
    [TextSearchResultValue]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 文档的唯一标识符
    /// </summary>
    [VectorStoreData]
    public string DocumentId { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// pdf:bookmark
    /// </summary>
    [VectorStoreData(IsFullTextIndexed = true)]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData] public int Level { get; set; }

    [VectorStoreData] public string ParentKey { get; set; } = string.Empty;

    /// <summary>
    /// indicate whether has child nodes. only used for chunk type 1 (node).
    /// </summary>
    [VectorStoreData]
    public bool HasChildNode { get; set; }

    /// <summary>
    /// 1: Bookmark 2: Paragraph
    /// </summary>
    [VectorStoreData]
    public int Type { get; set; }

    [VectorStoreVector(SemanticKernelStore.ChunkDimension)] public string TextEmbedding => string.IsNullOrEmpty(Text) ? " " : Text;

    [VectorStoreVector(SemanticKernelStore.ChunkDimension)] public string SummaryEmbedding => string.IsNullOrEmpty(Summary) ? " " : Summary;
}

public enum ChunkType : int
{
    Bookmark = 1,
    Paragraph = 2,
}