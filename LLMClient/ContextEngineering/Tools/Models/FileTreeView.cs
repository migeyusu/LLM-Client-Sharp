namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class FileTreeView
{
    public required string Root { get; set; }
    public int Depth { get; set; }
    public List<FileTreeNodeView> Nodes { get; set; } = new();
}