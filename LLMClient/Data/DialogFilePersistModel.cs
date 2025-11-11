namespace LLMClient.Data;

/// <summary>
/// 用于持久化
/// </summary>
public class DialogFilePersistModel : DialogSessionPersistModel
{
    public const int DialogPersistVersion = 3;

    public int Version { get; set; } = DialogPersistVersion;

    public DateTime EditTime { get; set; }
    
    public string Topic { get; set; } = string.Empty;

    public LLMClientPersistModel? Client { get; set; }
    
    public string? PromptString { get; set; }
}