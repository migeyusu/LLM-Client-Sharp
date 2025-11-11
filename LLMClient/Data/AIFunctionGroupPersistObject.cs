using LLMClient.Abstraction;

namespace LLMClient.Data;

public class AIFunctionGroupPersistObject
{
    public string[]? SelectedFunctionNames { get; set; }

    public IAIFunctionGroup? FunctionGroup { get; set; }
}