using System.Text.Json.Serialization;

namespace LLMClient.Data;

public class DialogSessionPersistModel
{
    public IDialogPersistItem[]? DialogItems { get; set; }

    [JsonPropertyName("SystemPrompt")]
    public string? UserSystemPrompt { get; set; }

    public PromptsPersistModel? ExtendedPrompts { get; set; }

    public long TokensConsumption { get; set; }

    public double TotalPrice { get; set; }

    public AIFunctionGroupPersistObject[]? AllowedFunctions { get; set; }
}

public class PromptsPersistModel
{
    public Guid[]? PromptReference { get; set; }
}