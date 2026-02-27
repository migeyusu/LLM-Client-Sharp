namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class TypeDetailExtra
{
    public List<string> BaseTypes { get; init; } = [];
    public List<string> ImplementedInterfaces { get; init; } = [];
    public bool IsPartial { get; init; }
    public bool IsAbstract { get; init; }
    public bool IsSealed { get; init; }
    public int MemberCount { get; init; }
}