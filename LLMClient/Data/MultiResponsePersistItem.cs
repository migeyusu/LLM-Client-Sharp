namespace LLMClient.Data;

public class MultiResponsePersistItem : BaseDialogPersistItem
{
    public Guid InteractionId { get; set; }
}

public class ParallelResponsePersisItem : MultiResponsePersistItem
{
    public int AcceptedIndex { get; set; }

    public ClientResponsePersistItem[] ResponseItems { get; set; } = [];
}

public class LinearHistoryResponsePersistItem : MultiResponsePersistItem
{
    public RawResponsePersistItem[] Items { get; set; } = [];
    public AgentPersistModel? Agent { get; set; }

    public bool IsManualValid { get; set; } = false;

    public bool IsAvailableInContextSwitch { get; set; } = true;
}