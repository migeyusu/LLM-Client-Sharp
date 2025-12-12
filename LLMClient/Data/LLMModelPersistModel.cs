namespace LLMClient.Data;

public class LLMModelPersistModel
{
    public string EndPointName { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;
    
    public override string ToString()
    {
        return $"{EndPointName} - {ModelName}";
    }
}