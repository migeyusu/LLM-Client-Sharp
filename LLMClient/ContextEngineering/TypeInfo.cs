namespace LLMClient.ContextEngineering;

public class TypeInfo
{
    public required string Name { get; set; }
    public required string FullName { get; set; }
    public required string Kind { get; set; }
    public required string Accessibility { get; set; }
    public List<MemberInfo> Members { get; } = new();
    public List<string> BaseTypes { get; } = new();
    public List<string> ImplementedInterfaces { get; set; } = new();
    public required string Summary { get; set; }
    public List<string> Attributes { get; set; } = new();
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