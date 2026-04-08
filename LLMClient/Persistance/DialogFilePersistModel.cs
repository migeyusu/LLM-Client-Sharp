using System.Text.Json.Serialization;
using LLMClient.Dialog;

namespace LLMClient.Persistance;

/// <summary>
/// 用于持久化
/// </summary>
public class DialogFilePersistModel : DialogSessionPersistModel
{
    public const int DialogPersistVersion = 3;

    public AgentOption? AgentOption { get; set; }

    public int Version { get; set; } = DialogPersistVersion;

    public DateTime EditTime { get; set; }

    public string? Topic { get; set; }

    public ParameterizedLLMModelPO? Client { get; set; }

    //自动映射
    [JsonPropertyName("SystemPrompt")] public string? UserSystemPrompt { get; set; }

    public PromptsPersistModel? ExtendedPrompts { get; set; }

    public string? PromptString { get; set; }


    public bool IsFunctionEnabled { get; set; }
}