using System.Text.Json.Serialization;
using LLMClient.Abstraction;

namespace LLMClient.Persistance;

public class AIFunctionGroupPersistObject
{
    public string[]? SelectedFunctionNames { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AIFunctionGroupDefinitionPersistModel? FunctionGroupPersistModel { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IAIFunctionGroup? FunctionGroup { get; set; }
}