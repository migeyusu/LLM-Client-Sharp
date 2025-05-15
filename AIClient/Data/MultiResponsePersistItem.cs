namespace LLMClient.Data;

public class MultiResponsePersistItem : IDialogItem
{
    public ResponsePersistItem[] ResponseItems { get; set; } = [];

    public int AcceptedIndex { get; set; }
}