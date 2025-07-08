using LLMClient.UI;

namespace LLMClient.Data;

/// <summary>
/// 用于持久化
/// </summary>
public class DialogSessionPersistModel : DialogPersistModel
{
    public const int DialogPersistVersion = 3;

    public int Version { get; set; } = DialogPersistVersion;

    public DateTime EditTime { get; set; }
}