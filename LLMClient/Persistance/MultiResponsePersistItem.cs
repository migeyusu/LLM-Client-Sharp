using System.Text.Json.Serialization;

namespace LLMClient.Persistance;

public class MultiResponsePersistItem : BaseDialogPersistItem
{
    public Guid InteractionId { get; set; }
}

public class ParallelResponsePersisItem : MultiResponsePersistItem
{
    public int AcceptedIndex { get; set; }

    public ClientResponsePersistItem[] ResponseItems { get; set; } = [];
}

public class LinearHistoryResponsePersistItem : BaseDialogPersistItem
{
    public Guid InteractionId { get; set; }

    public RawResponsePersistItem? Response { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RawResponsePersistItem[]? Items { get; set; }

    public AgentPersistModel? Agent { get; set; }

    public bool IsManualValid { get; set; } = false;

    public bool IsAvailableInContextSwitch { get; set; } = true;
}