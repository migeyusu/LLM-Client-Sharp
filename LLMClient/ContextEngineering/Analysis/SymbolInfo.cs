namespace LLMClient.ContextEngineering.Analysis;

public abstract class SymbolInfo
{
    public string? UniqueId { get; set; }
    public required string Name { get; set; }
    public required string Signature { get; set; }
    public required string Kind { get; set; }
    public required string Accessibility { get; set; }
    public List<string> Attributes { get; set; } = new();
    public string? Summary { get; set; }
    public required List<CodeLocation> Locations { get; set; }

    public IEnumerable<string> FilesPath
    {
        get { return Locations.Select(location => location.FilePath); }
    }
}