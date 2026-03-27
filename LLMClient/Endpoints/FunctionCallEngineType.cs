using System.ComponentModel;
using System.Text.Json.Serialization;
using LLMClient.Component.Converters;

namespace LLMClient.Endpoints;

[TypeConverter(typeof(EnumDescriptionTypeConverter))]
[JsonConverter(typeof(JsonStringEnumConverter<FunctionCallEngineType>))]
public enum FunctionCallEngineType
{
    [Description("OpenAI API")] OpenAI,

    [Description("Prompt-based")] Prompt,

    [Description("mini-SWE")] MiniSwe,
}