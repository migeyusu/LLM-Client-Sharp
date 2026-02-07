using LLMClient.Project;

namespace LLMClient.Data;

public class ProjectTaskPersistModel : DialogSessionPersistModel
{
    public string? Name { get; set; }

    public string? Summary { get; set; }
    
    public bool EnableInContext { get; set; }
}