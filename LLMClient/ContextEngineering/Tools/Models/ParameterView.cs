namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class ParameterView
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public string? DefaultValue { get; init; }
}