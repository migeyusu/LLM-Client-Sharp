using LLMClient.Abstraction;

namespace LLMClient.Data;

public class ParameterizedLLMModelPO : LLMModelPersistModel
{
    public IModelParams? Params { get; set; }
}