using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Endpoints;
using LLMClient.UI.Dialog;
using Microsoft.Extensions.AI;

namespace LLMClient.Data;

public class RequestPersistItem : IDialogPersistItem
{
    public Guid InteractionId { get; set; }

    [JsonPropertyName("MessageContent")] public string? TextMessage { get; set; }

    public List<AIFunctionGroupPersistObject>? FunctionGroups { get; set; }

    public ISearchService? SearchService { get; set; }

    /// <summary>
    /// 对Request附加的额外属性
    /// </summary>
    [JsonPropertyName("RequestAdditionalProperties")]
    public AdditionalPropertiesDictionary AdditionalProperties { get; set; } = new AdditionalPropertiesDictionary();

    public List<Attachment>? Attachments { get; set; }

    public IThinkingConfig? ThinkingConfig { get; set; }
}