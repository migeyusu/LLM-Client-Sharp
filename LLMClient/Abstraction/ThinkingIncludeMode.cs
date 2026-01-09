using System.ComponentModel;
using System.Text.Json.Serialization;
using LLMClient.Component.Converters;
using LLMClient.Rag;

namespace LLMClient.Abstraction;

[TypeConverter(typeof(EnumDescriptionTypeConverter))]
[JsonConverter(typeof(JsonStringEnumConverter<SearchAlgorithm>))]
public enum ThinkingIncludeMode
{
    /// <summary>
    /// include nothing between multi-turn dialogues
    /// </summary>
    [Description("不包含思考内容")]
    None = 0,

    /// <summary>
    /// always include all thinking content between multi-turn dialogues
    /// </summary>
    [Description("包含所有思考内容")]
    All = 1,

    /// <summary>
    /// only keep the last dialogue's thinking content between multi-turn dialogues
    /// </summary>
    [Description("仅保留最后一次思考内容")]
    KeepLast = 2,
}