namespace LLMClient.ContextEngineering;

public class AnalyzerConfig
{
    public bool IncludeTestProjects { get; set; } = false;
    public bool IncludeSampleProjects { get; set; } = false;
    public bool IncludePrivateMembers { get; set; } = false;
    public bool IncludeInternalMembers { get; set; } = true;
    public bool IncludeForwardingMethods { get; set; } = false;
    public int MaxConcurrency { get; set; } = 4;

    public List<string> ExcludedPatterns { get; set; } = new()
    {
        "obj/",
        "bin/",
        ".vs/",
        "Generated/",
        "Migrations/",
        ".g.cs",
        ".Designer.cs"
    };
}