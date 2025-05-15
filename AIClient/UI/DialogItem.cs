using System.Text.Json.Serialization;
using System.Windows.Documents;
using System.Windows.Media;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using Microsoft.Extensions.AI;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace LLMClient.UI;

public interface IDialogViewItem
{
    ChatMessage? Message { get; }

    bool IsAvailableInContext { get; }

    long Tokens { get; }
}

public class EraseViewItem : IDialogViewItem, IDialogItem
{
    [JsonIgnore] public ChatMessage? Message { get; } = null;

    /// <summary>
    /// 是否在上下文中有效
    /// </summary>
    [JsonPropertyName("IsEnable")]
    public bool IsAvailableInContext { get; } = false;

    public long Tokens { get; } = 0;
}

public class RequestViewItem : BaseViewModel, IDialogViewItem, IDialogItem
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
    public ImageSource Icon
    {
        get { return Model?.Icon ?? APIClient.IconImageSource; }
    }

    public string EndPointName { get; }

    public string ModelName
    {
        get { return Model?.Name ?? string.Empty; }
    }

    public ILLMModel? Model { get; }

    /// <summary>
    /// 是否中断
    /// </summary>
    public bool IsInterrupt { get; }

    public long Tokens { get; }

    public string? ErrorMessage { get; }

    private FlowDocument? _flowDocument = null;

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

    public string? Raw { get; }


    public ResponseViewItem(ILLMModel? model, string? raw, long tokens, bool interrupt,
        string? errorMessage, string endPointName)
    {
        Model = model;
        Raw = raw;
        Tokens = tokens;
        IsInterrupt = interrupt;
        ErrorMessage = errorMessage;
        EndPointName = endPointName;
    }

    public ResponseViewItem(ILLMModelClient client, CompletedResult result)
    {
        Model = client.Info;
        EndPointName = client.Endpoint.Name;
        Raw = result.Response;
        Tokens = result.Usage.OutputTokenCount ?? 0;
        IsInterrupt = result.IsInterrupt;
        ErrorMessage = result.ErrorMessage;
    }

    private ChatMessage? _assistantMessage;

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

    public ChatMessage? Message => AssistantMessage;


    public bool IsAvailableInContext
    {
        get { return !IsInterrupt; }
    }
}