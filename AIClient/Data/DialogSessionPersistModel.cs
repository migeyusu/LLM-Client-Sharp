using LLMClient.Abstraction;

namespace LLMClient.Data;

public class DialogSessionPersistModel
{
    public IDialogPersistItem[]? DialogItems { get; set; }

    public string? PromptString { get; set; }

    public string? SystemPrompt { get; set; }

    public long TokensConsumption { get; set; }

    public double TotalPrice { get; set; }

    public IAIFunctionGroup[]? Functions { get; set; }
}