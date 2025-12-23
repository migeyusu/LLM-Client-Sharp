namespace LLMClient.ContextEngineering;

public class SummaryStatistics : ProjectStatistics
{
    public int TotalProjects { get; set; }
    public Dictionary<string, int> TypeDistribution { get; set; } = new();
}