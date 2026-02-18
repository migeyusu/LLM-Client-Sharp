namespace LLMClient.ContextEngineering.Analysis;

public class ParameterInfo
{
    public required string Name { get; set; }
    public required string Type { get; set; }
    public bool HasDefaultValue { get; set; }
    public required string? DefaultValue { get; set; }
}