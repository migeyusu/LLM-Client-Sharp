using Microsoft.CodeAnalysis.Text;

namespace LLMClient.ContextEngineering.Analysis;

public class MemberInfo: SymbolInfo
{
    public bool IsStatic { get; set; }
    public bool IsAsync { get; set; }
    public bool IsVirtual { get; set; }
    public bool IsOverride { get; set; }
    public string? ReturnType { get; set; }
    public List<ParameterInfo>? Parameters { get; set; }
    public string? Comment { get; set; }
}

public class CodeLocation
{
    public required string FilePath { get; set; }

    public LinePositionSpan Location { get; set; }
}