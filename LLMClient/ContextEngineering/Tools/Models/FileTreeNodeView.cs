namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class FileTreeNodeView
{
    public required string Name { get; set; }
    public required string Path { get; set; } // absolute
    public bool IsFile { get; set; }
    public List<FileTreeNodeView> Children { get; set; } = new();
}