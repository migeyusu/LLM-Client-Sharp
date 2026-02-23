using System.Text.Json.Serialization;

namespace LLMClient.ContextEngineering.Analysis;

public class SolutionInfo
{
    public required string SolutionName { get; set; }
    public required string SolutionPath { get; set; }
    public List<ProjectInfo> Projects { get; set; } = new();
    public DateTime GeneratedAt { get; set; }

    public string Version { get; set; } = "1.0";
    public SummaryStatistics Statistics { get; set; } = new();

    [JsonIgnore] public TimeSpan GenerationTime { get; set; }
    
    public ConventionInfo Conventions { get; set; } = new();
}