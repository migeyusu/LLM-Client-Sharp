using LLMClient.Project;

namespace LLMClient.Data;

public class ProjectPersistModel
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public DateTime EditTime { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string[]? LanguageNames { get; set; }


    public string[]? AllowedFolderPaths { get; set; }

    public string? FolderPath { get; set; }

    #region requester

    public LLMClientPersistModel? Client { get; set; }

    #endregion

    public long TokensConsumption { get; set; }

    public double TotalPrice { get; set; }

    public ProjectTaskPersistModel[]? Tasks { get; set; }
}

public class ProjectTaskPersistModel : DialogSessionPersistModel
{
    public string? Name { get; set; }

    public string? Summary { get; set; }
    
    public bool EnableInContext { get; set; }
    
    public string? PromptString { get; set; }
    
    public string? Description { get; set; }

    public ProjectTaskType Type { get; set; }
}