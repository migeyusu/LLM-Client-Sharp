namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class MemberDetailExtra
{
    public string? ReturnType { get; init; }
    public List<ParameterView>? Parameters { get; init; }
    public bool IsStatic { get; init; }
    public bool IsAsync { get; init; }
    public bool IsVirtual { get; init; }
    public bool IsOverride { get; init; }
    public string? ContainingType { get; init; }
}