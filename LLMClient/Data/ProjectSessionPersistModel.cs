namespace LLMClient.Data;

public class ProjectSessionPersistModel : DialogSessionPersistModel
{
    public string? Name { get; set; }

    public string? Summary { get; set; }
    
    public bool EnableInContext { get; set; }
}