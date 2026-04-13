namespace LLMClient.Persistence;

public class SummaryRequestPersistItem : BaseDialogPersistItem
{
    public int OutputLength { get; set; }

    public string? SummaryPrompt { get; set; }

    public Guid InteractionId { get; set; }
}