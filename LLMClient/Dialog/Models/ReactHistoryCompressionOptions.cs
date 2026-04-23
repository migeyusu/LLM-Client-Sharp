using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel.Base;

namespace LLMClient.Dialog.Models;

public class ReactHistoryCompressionOptions : BaseViewModel
{
    [JsonPropertyName("Mode")]
    public ReactHistoryCompressionMode Mode
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged();
        }
    } = ReactHistoryCompressionMode.None;

    /// <summary>
    /// Token threshold (as a fraction of the model's maximum context size) that triggers in-task
    /// history compression. When the estimated token count of the current chat history exceeds
    /// <c>ReactTokenThresholdPercent * model.MaxContextSize</c>, the active compression strategy
    /// is invoked. A value of 0 or less disables token-based compression triggering.
    /// <para>Range:[0-1]</para>
    /// </summary>
    [JsonPropertyName("ReactTokenThresholdPercent")]
    public double ReactTokenThresholdPercent
    {
        get;
        set
        {
            if (value.Equals(field)) return;
            field = value;
            OnPropertyChanged();
        }
    } = 0.8;

    [JsonPropertyName("PreserveRecentRounds")]
    public int PreserveRecentRounds
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = 6;

    [JsonPropertyName("ObservationPlaceholder")]
    public string ObservationPlaceholder
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = "[details omitted for brevity]";

    /// <summary>
    /// When enabled, preamble messages (previous task context without round tags)
    /// are summarized once before the ReAct loop starts.
    /// </summary>
    [JsonPropertyName("PreambleCompression")]
    public bool PreambleCompression
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Estimated token threshold above which preamble compression is triggered.
    /// Only the historical messages between system prompt and current user message
    /// are counted.
    /// <para>Range:[0-1]</para> 
    /// </summary>
    [JsonPropertyName("PreambleTokenThreshold")]
    public double PreambleTokenThresholdPercent
    {
        get;
        set
        {
            if (value.Equals(field)) return;
            field = value;
            OnPropertyChanged();
        }
    } = 0.8;

    /// <summary>
    /// whether to summary error function call messages from the history, as they may contain long error details that are not useful for the model to see and may cause token overflow.
    /// <para>only enabled after <see cref="Mode"/> is not <see cref="ReactHistoryCompressionMode.None"/></para>
    /// </summary>
    [JsonPropertyName("SummaryErrorLoop")]
    public bool SummaryErrorLoop
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }
}