using System.Text.Json.Serialization;

namespace LLMClient.Abstraction;

public class ReactHistoryCompressionOptions
{
    [JsonPropertyName("Mode")] public ReactHistoryCompressionMode Mode { get; set; } = ReactHistoryCompressionMode.None;

    [JsonPropertyName("PreserveRecentRounds")]
    public int PreserveRecentRounds { get; set; } = 2;

    [JsonPropertyName("ObservationPlaceholder")]
    public string ObservationPlaceholder { get; set; } = "[details omitted for brevity]";

    /// <summary>
    /// When enabled, preamble messages (previous task context without round tags)
    /// are summarized once before the ReAct loop starts.
    /// </summary>
    [JsonPropertyName("PreambleCompression")]
    public bool PreambleCompression { get; set; }

    /// <summary>
    /// Estimated token threshold above which preamble compression is triggered.
    /// Only the historical messages between system prompt and current user message
    /// are counted. Uses character-based estimation (length / 2.8).
    /// </summary>
    [JsonPropertyName("PreambleTokenThreshold")]
    public double PreambleTokenThresholdPercent { get; set; }

    /// <summary>
    /// whether to summary error function call messages from the history, as they may contain long error details that are not useful for the model to see and may cause token overflow.
    /// </summary>
    [JsonPropertyName("SummaryErrorLoop")]
    public bool SummaryErrorLoop { get; set; }
}