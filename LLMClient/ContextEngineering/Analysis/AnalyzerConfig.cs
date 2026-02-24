namespace LLMClient.ContextEngineering.Analysis;

public class AnalyzerConfig
{
    public bool IncludeTestProjects { get; set; } = true;
    public bool IncludeSampleProjects { get; set; } = false;
    public bool IncludePrivateMembers { get; set; } = false;
    public bool IncludeInternalMembers { get; set; } = true;
    public int MaxConcurrency { get; set; } = 4;   
    
    public bool IncludeForwardingMethods { get; set; } = false;

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