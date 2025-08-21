namespace LLMClient.Rag;

/// <summary>
/// used as viewmodel
/// </summary>
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
        if (this.Chunk.Type == (int)ChunkType.Page)
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
        else if (Chunk.Type == (int)ChunkType.Page)
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