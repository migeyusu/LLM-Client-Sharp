namespace LLMClient.Data;

public class MultiResponsePersistItem : BaseDialogPersistItem
{
    public ResponsePersistItem[] ResponseItems { get; set; } = [];

    public int AcceptedIndex { get; set; }

    public Guid InteractionId { get; set; }
}