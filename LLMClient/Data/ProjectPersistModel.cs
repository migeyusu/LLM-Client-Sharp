using System.Text.Json.Serialization;

namespace LLMClient.Data;

public class ProjectPersistModel
{
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;

    public string? Type { get; set; }

    public DateTime EditTime { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public PromptsPersistModel? ExtendedPrompts { get; set; }

    public string[]? AllowedFolderPaths { get; set; }

    public string? FolderPath { get; set; }
    
    [JsonPropertyName("SystemPrompt")]
    public string? UserSystemPrompt { get; set; }

    #region requester

    public ParameterizedLLMModelPO? Client { get; set; }

    public string? UserPrompt { get; set; }

    #endregion

    public long TokensConsumption { get; set; }

    public double TotalPrice { get; set; }

    public ProjectTaskPersistModel[]? Tasks { get; set; }
}

public class CSharpProjectPersistModel : ProjectPersistModel
{
    public bool IsSolutionMode { get; set; }

    public string? SolutionFilePath { get; set; }

    public string? ProjectFilePath { get; set; }
}

public class CppProjectPersistModel : ProjectPersistModel
{
}