namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class ProjectStatsView
{
    public int FilesCount { get; set; }
    public int TypesCount { get; set; }
    public int MethodsCount { get; set; }
    public int LinesOfCode { get; set; }
}