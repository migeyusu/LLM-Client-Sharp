using System.ComponentModel;
using System.Text.Json.Serialization;
using LLMClient.Component.Converters;

namespace LLMClient.Abstraction;

[TypeConverter(typeof(EnumDescriptionTypeConverter))]
[JsonConverter(typeof(JsonStringEnumConverter<ReactHistoryCompressionMode>))]
public enum ReactHistoryCompressionMode
{
    [Description("No Compression")] None,
    
    [Description("Summary All")] TaskSummary,

    [Description("Masking Observation")] ObservationMasking,

    [Description("Summary Each Loop")] InfoCleaning,
}