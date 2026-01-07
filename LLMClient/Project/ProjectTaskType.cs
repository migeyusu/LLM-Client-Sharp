using System.ComponentModel;
using System.Text.Json.Serialization;
using LLMClient.Component.Converters;

namespace LLMClient.Project;

[TypeConverter(typeof(EnumDescriptionTypeConverter))]
[JsonConverter(typeof(JsonStringEnumConverter<ProjectTaskType>))]
public enum ProjectTaskType : int
{
    [Description("需求变更")] NewDemand,
    [Description("修复Bug")] BugFix,
    [Description("代码翻译")] Translation,
    [Description("代码重构")] CodeRefactor,
    [Description("代码审查")] CodeReview,
    [Description("代码生成")] CodeGeneration,
    [Description("代码优化")] CodeOptimization,
    [Description("代码文档编写")] CodeDocumentation,
    [Description("单元测试编写")] UnitTestConstruction,
}