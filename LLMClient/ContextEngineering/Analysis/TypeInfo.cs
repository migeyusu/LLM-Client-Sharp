namespace LLMClient.ContextEngineering.Analysis;

public class TypeInfo : SymbolInfo
{
    public List<MemberInfo> Members { get; } = new();
    public List<string> BaseTypes { get; } = new();
    public List<string> ImplementedInterfaces { get; set; } = new();
    public bool IsPartial { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsSealed { get; set; }
}