using System.Text.Json.Serialization;

namespace LLMClient.Data;

public abstract class ProjectPersistModel
{
    public const int CurrentVersion = 3;

    public int Version { get; set; } = CurrentVersion;

    public DateTime EditTime { get; set; }

    [JsonPropertyName("ProjectOptions")] public ProjectOptionsPersistModel? Option { get; set; }

    public PromptsPersistModel? ExtendedPrompts { get; set; }

    [JsonPropertyName("SystemPrompt")] public string? UserSystemPrompt { get; set; }

    #region requester

    public ParameterizedLLMModelPO? Client { get; set; }

    public string? UserPrompt { get; set; }

    #endregion

    public long TokensConsumption { get; set; }

    public double TotalPrice { get; set; }

    public ProjectSessionPersistModel[]? Tasks { get; set; }
}

public class GeneralProjectPersistModel : ProjectPersistModel
{
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