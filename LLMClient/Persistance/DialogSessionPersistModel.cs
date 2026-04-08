namespace LLMClient.Data;

public class DialogSessionPersistModel
{
    public IDialogPersistItem[]? DialogItems { get; set; }

    public IDialogPersistItem? RootNode { get; set; }

    public IDialogPersistItem? CurrentLeaf { get; set; }

    public long TokensConsumption { get; set; }

    public double TotalPrice { get; set; }

    public AIFunctionGroupPersistObject[]? AllowedFunctions { get; set; }
}