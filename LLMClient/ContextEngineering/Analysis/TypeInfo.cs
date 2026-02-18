namespace LLMClient.ContextEngineering.Analysis;

public class TypeInfo : SymbolInfo
{
    public List<MemberInfo> Members { get; } = new();
    public List<string> BaseTypes { get; } = new();
    public List<string> ImplementedInterfaces { get; set; } = new();
    public bool IsPartial { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsSealed { get; set; }
    public required string FilePath { get; set; }

    /// <summary>
    /// relative to project file
    /// </summary>
    public required string RelativePath { get; set; }

    public int LineNumber { get; set; }
}