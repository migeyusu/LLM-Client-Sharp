namespace LLMClient.ContextEngineering;

public class MemberInfo
{
    public required string Name { get; set; }
    public required string Kind { get; set; }
    public required string Accessibility { get; set; }
    public required string Signature { get; set; }
    public bool IsStatic { get; set; }
    public bool IsAsync { get; set; }
    public bool IsVirtual { get; set; }
    public bool IsOverride { get; set; }
    public required string ReturnType { get; set; }
    public List<ParameterInfo> Parameters { get; set; } = new();
    public required string Comment { get; set; }
    public List<string> Attributes { get; set; } = new();
}