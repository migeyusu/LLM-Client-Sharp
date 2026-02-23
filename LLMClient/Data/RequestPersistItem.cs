using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;

namespace LLMClient.Data;

public class RequestPersistItem : BaseDialogPersistItem
{
    public Guid InteractionId { get; set; }

    [JsonPropertyName("MessageContent")] public string? RawTextMessage { get; set; }

    public string? FormattedTextMessage { get; set; }

    public List<AIFunctionGroupPersistObject>? FunctionGroups { get; set; }

    public IList<IRagSource>? RagSources { get; set; }

    public ISearchOption? SearchService { get; set; }

    public ChatResponseFormat? ResponseFormat { get; set; }

    public FunctionCallEngineType CallEngine { get; set; }
    
    public long Tokens { get; set; }

    /// <summary>
    /// 对Request附加的额外属性
    /// </summary>
    // [JsonPropertyName("RequestAdditionalProperties")]
    // public AdditionalPropertiesDictionary AdditionalProperties { get; set; } = new();

    public List<Attachment>? Attachments { get; set; }

    public IThinkingConfig? ThinkingConfig { get; set; }
}