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
public interface IDialogViewItem
{
    [JsonIgnore] ChatMessage? Message { get; }

    bool IsEnable { get; }
}

public class EraseViewItem : IDialogViewItem
{
    [JsonIgnore] public ChatMessage? Message { get; } = null;

    public bool IsEnable { get; } = false;
}

public class RequestViewItem : IDialogViewItem
{
    public RequestViewItem() : base()
    {
    }

    public string MessageContent { get; set; } = string.Empty;

    [JsonIgnore] public ChatMessage? Message => new ChatMessage(ChatRole.User, MessageContent);

    public bool IsEnable { get; set; } = true;
}

public class ResponseViewItem : IDialogViewItem
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

    private ChatMessage? _assistantMessage;

    [JsonIgnore]
    public ChatMessage? AssistantMessage
    {
        get
        {
            if (Raw == null)
            {
                return null;
            }

            if (_assistantMessage == null)
            {
                _assistantMessage = new ChatMessage(ChatRole.Assistant, Raw);
            }

            return _assistantMessage;
        }
    }

    [JsonIgnore] public ChatMessage? Message => AssistantMessage;

    public bool IsEnable
    {
        get { return !IsInterrupt; }
    }
}