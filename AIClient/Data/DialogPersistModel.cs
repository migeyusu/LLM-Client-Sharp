using LLMClient.Abstraction;
using LLMClient.UI;

namespace LLMClient.Data;

/// <summary>
/// 用于持久化
/// </summary>
public class DialogPersistModel
{
    public const int DialogPersistVersion = 2;

    public int Version { get; set; } = DialogPersistVersion;

    public DateTime EditTime { get; set; }

    public IDialogPersistItem[]? DialogItems { get; set; }

    public string Topic { get; set; } = string.Empty;

    public string? EndPoint { get; set; }

    public string? Model { get; set; }

    public string? PromptString { get; set; }

    public IModelParams? Params { get; set; }

    public long TokensConsumption { get; set; }
}