namespace LLMClient.Data;

public class BaseDialogPersistItem : IDialogPersistItem
{
    public Guid Id { get; set; }

    public Guid? PreviousItemId { get; set; }
}