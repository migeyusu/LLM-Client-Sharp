namespace LLMClient.ContextEngineering.Prompt;

// 内部使用的结果模型

public class FormatterOptions
{
    public bool IncludeDependencies { get; set; } = true;
    public bool IncludePackages { get; set; } = true;
    public bool IncludeTypes { get; set; } = true;
    public bool IncludeMembers { get; set; } = false;
    public bool IncludeSummaries { get; set; } = true;
    public int MaxPackagesToShow { get; set; } = 10;
    public int MaxTypesPerNamespace { get; set; } = 10;
    public int MaxMembersPerType { get; set; } = 5;
}