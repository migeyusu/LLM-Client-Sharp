using LLMClient.Abstraction;

namespace LLMClient.Data;

public class DialogPersistModel
{
    public IDialogPersistItem[]? DialogItems { get; set; }

    public string Topic { get; set; } = string.Empty;

    public string? EndPoint { get; set; }

    public string? Model { get; set; }

    public string? PromptString { get; set; }
    
    public string? SystemPrompt { get; set; }

    public IModelParams? Params { get; set; }

    public long TokensConsumption { get; set; }

    public double TotalPrice { get; set; }

    public IAIFunctionGroup[]? Functions { get; set; }
}