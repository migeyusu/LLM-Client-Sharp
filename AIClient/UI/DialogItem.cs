using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Documents;
using System.Windows.Input;
using Azure.AI.Inference;
using LLMClient.Render;
using Markdig;
using Markdig.Wpf;
using Microsoft.Extensions.AI;
using Microsoft.Xaml.Behaviors.Core;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace LLMClient.UI;

[JsonDerivedType(typeof(EraseViewItem), "erase")]
[JsonDerivedType(typeof(RequestViewItem), "request")]
[JsonDerivedType(typeof(ResponseViewItem), "response")]
public interface IDialogViewItem
{
    [JsonIgnore] ChatMessage? Message { get; }

    bool IsAvailableInContext { get; }

    long Tokens { get; }
}

public class EraseViewItem : IDialogViewItem
{
    [JsonIgnore] public ChatMessage? Message { get; } = null;

    /// <summary>
    /// 是否在上下文中有效
    /// </summary>
    [JsonPropertyName("IsEnable")]
    public bool IsAvailableInContext { get; } = false;

    public long Tokens { get; } = 0;
}

public class RequestViewItem : BaseViewModel, IDialogViewItem
{
    private long _tokens;

    public RequestViewItem() : base()
    {
    }

    public string MessageContent { get; set; } = string.Empty;

    [JsonIgnore] public ChatMessage? Message => new ChatMessage(ChatRole.User, MessageContent);

    [JsonPropertyName("IsEnable")] public bool IsAvailableInContext { get; set; } = true;

    public long Tokens
    {
        get => _tokens;
        set
        {
            if (value == _tokens) return;
            _tokens = value;
            OnPropertyChanged();
        }
    }
}

public class ResponseViewItem : BaseViewModel, IDialogViewItem
{
    /// <summary>
    /// 是否中断
    /// </summary>
    public bool IsInterrupt { get; set; }

    public long Tokens { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 是否为多回复
    /// </summary>
    public bool IsMultiResponse { get; set; }

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

    public ObservableCollection<ResponseItem> ResponseItems { get; set; }

    public ResponseItem CurrentItem { get; set; }

    public ICommand SelectItemCommand =>
        new ActionCommand((o =>
        {
            if (o is ResponseItem item)
            {
                Raw = item.Raw;
                IsMultiResponse = false;
                _flowDocument = null;
                OnPropertyChanged(nameof(Document));
            }
        }));

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

    [JsonPropertyName("IsEnable")]
    public bool IsAvailableInContext
    {
        get { return !IsInterrupt; }
    }
}

public class ResponseItem
{
    //这里不绑定到APIClient，没有必要

    public string Name { get; set; } = string.Empty;

    public string? Raw { get; set; }

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

    public string? ErrorMessage { get; set; }

    public bool IsInterrupt { get; set; }

    public long Tokens { get; set; }
}