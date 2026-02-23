namespace LLMClient.ContextEngineering.Analysis;

public class SummaryStatistics : ProjectStatistics
{
    public int TotalProjects { get; set; }
    public Dictionary<string, int> TypeDistribution { get; set; } = new();
}