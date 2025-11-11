namespace LLMClient.Data;

public class MultiResponsePersistItem : IDialogPersistItem
{
    public ResponsePersistItem[] ResponseItems { get; set; } = [];

    public int AcceptedIndex { get; set; }

    public Guid InteractionId { get; set; }
}