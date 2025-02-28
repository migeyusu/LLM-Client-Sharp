

namespace LLMClient.UI;

/// <summary>
/// 用于持久化
/// </summary>
public class DialogModel
{
    public Guid DialogId { get; set; }

    public IDialogViewItem[]? DialogItems { get; set; }

    public string Topic { get; set; } = string.Empty;

    public string? EndPoint { get; set; }

    public string? Model { get; set; }

    public string? PromptString { get; set; }

    public IModelParams? Params { get; set; }
}