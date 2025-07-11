using LLMClient.Abstraction;
using LLMClient.UI.Project;

namespace LLMClient.Data;

public class ProjectPersistModel
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string[]? LanguageNames { get; set; }


    public string[]? AllowedFolderPaths { get; set; }

    public string? FolderPath { get; set; }

    public LLMClientPersistModel? Client { get; set; }

    public long TokensConsumption { get; set; }

    public float TotalPrice { get; set; }

    /// <summary>
    /// 额外的mcp工具
    /// </summary>
    public IAIFunctionGroup[]? AllowedFunctions { get; set; }

    public ProjectTaskPersistModel[]? Tasks { get; set; }
}

public class ProjectTaskPersistModel
{
    public string? Name { get; set; }

    public string? Summary { get; set; }

    public ProjectTaskType Type { get; set; }

    public ProjectTaskStatus Status { get; set; }

    public IDialogPersistItem[]? DialogItems { get; set; }

    public string? PromptString { get; set; }

    public string? SystemPrompt { get; set; }

    public long TokensConsumption { get; set; }

    public double TotalPrice { get; set; }
}