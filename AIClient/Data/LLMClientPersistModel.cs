using LLMClient.Abstraction;

namespace LLMClient.Data;

public class LLMClientPersistModel
{
    public string EndPointName { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    public IModelParams? Params { get; set; }

    public override string ToString()
    {
        return $"{EndPointName} - {ModelName}";
    }
}