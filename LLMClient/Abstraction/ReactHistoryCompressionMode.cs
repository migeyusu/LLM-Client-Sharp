using System.Text.Json.Serialization;

namespace LLMClient.Abstraction;

[JsonConverter(typeof(JsonStringEnumConverter<ReactHistoryCompressionMode>))]
public enum ReactHistoryCompressionMode
{
    None,
    TaskSummary,
    ObservationMasking,
    InfoCleaning,
}

