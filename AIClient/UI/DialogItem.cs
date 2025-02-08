using System.Text.Json.Serialization;
using System.Windows.Documents;
using Azure.AI.Inference;
using LLMClient.Render;
using Markdig;
using Markdig.Wpf;
using Microsoft.Extensions.AI;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace LLMClient.UI;

[JsonDerivedType(typeof(EraseViewItem), "erase")]
[JsonDerivedType(typeof(RequestViewItem), "request")]
[JsonDerivedType(typeof(ResponseViewItem), "response")]
public abstract class DialogViewItem
{
    public DialogViewItem()
    {
    }

    [JsonIgnore] public abstract ChatRequestMessage? Message { get; }
}

public class EraseViewItem : DialogViewItem
{
    [JsonIgnore] public override ChatRequestMessage? Message { get; } = null;
}

public class RequestViewItem : DialogViewItem
{
    public RequestViewItem() : base()
    {
    }

    [JsonIgnore]
    public ChatRequestUserMessage? UserMessage
    {
        get { return new ChatRequestUserMessage(MessageContent); }
    }

    public string MessageContent { get; set; } = string.Empty;

    [JsonIgnore] public override ChatRequestMessage? Message => UserMessage;
}

public class ResponseViewItem : DialogViewItem
{
    /// <summary>
    /// 是否中断
    /// </summary>
    public bool IsInterrupt { get; set; }

    public string? ErrorMessage { get; set; }

    private FlowDocument? _flowDocument = null;

    [JsonIgnore]
    public FlowDocument? Document
    {
        get
        {
            if (Raw == null)
            {
                return null;
            }

            if (_flowDocument == null)
            {
                _flowDocument = this.Raw.ToFlowDocument();
            }

            return _flowDocument;
        }
    }

    public string? Raw { get; set; }


    public ResponseViewItem()
    {
        
    }

    private ChatRequestAssistantMessage? _assistantMessage;

    [JsonIgnore]
    public ChatRequestAssistantMessage? AssistantMessage
    {
        get
        {
            if (Raw == null)
            {
                return null;
            }

            if (_assistantMessage == null)
            {
                _assistantMessage = new ChatRequestAssistantMessage(Raw);
            }

            return _assistantMessage;
        }
    }

    [JsonIgnore] public override ChatRequestMessage? Message => AssistantMessage;
}