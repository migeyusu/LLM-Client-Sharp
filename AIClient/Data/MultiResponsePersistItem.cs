namespace LLMClient.Data;

public class MultiResponsePersistItem : IDialogPersistItem
{
    public ResponsePersistItem[] ResponseItems { get; set; } = [];

    public int AcceptedIndex { get; set; }
}