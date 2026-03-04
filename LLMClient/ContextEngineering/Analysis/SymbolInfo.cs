namespace LLMClient.ContextEngineering.Analysis;

public abstract class SymbolInfo
{
    public string? UniqueId { get; set; }
    public required string Name { get; set; }
    public required string Signature { get; set; }
    
    /// <summary>
    /// kind or type of the symbol, e.g. class, method, property, field, event, interface, enum, struct, delegate, namespace等
    /// </summary>
    public required string Kind { get; set; }
    public required string Accessibility { get; set; }
    public List<string> Attributes { get; set; } = [];
    public string? Summary { get; set; }
    public required List<CodeLocation> Locations { get; set; }

    public string GetRelativePath(string basePath)
    {
        var firstOrDefault = FilesPath.FirstOrDefault();
        return firstOrDefault != null ? Path.GetRelativePath(basePath, firstOrDefault) : string.Empty;
    }

    public string StartLineNumber()
    {
        var firstOrDefault = Locations.FirstOrDefault();
        return firstOrDefault != null ? firstOrDefault.Location.Start.Line.ToString() : string.Empty;
    }

    public IEnumerable<string> FilesPath
    {
        get { return Locations.Select(location => location.FilePath); }
    }

    /// <summary>
    /// 用于唯一标识一个符号，优先使用UniqueId，如果没有则使用Signature
    /// </summary>
    public string SymbolId
    {
        get { return UniqueId ?? Signature; }
    }
}