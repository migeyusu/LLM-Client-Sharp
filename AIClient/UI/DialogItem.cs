﻿using System.Text.Json.Serialization;
using System.Windows.Documents;
using System.Windows.Media;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.UI.Component;
using Microsoft.Extensions.AI;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace LLMClient.UI;

public interface IDialogViewItem
{
    ChatMessage? Message { get; }

    bool IsAvailableInContext { get; }
}

public class EraseViewItem : IDialogViewItem, IDialogPersistItem
{
    [JsonIgnore] public ChatMessage? Message { get; } = null;

    /// <summary>
    /// 是否在上下文中有效
    /// </summary>
    [JsonPropertyName("IsEnable")]
    public bool IsAvailableInContext { get; } = false;
}

public class RequestViewItem : BaseViewModel, IDialogViewItem, IDialogPersistItem
{
    /// <summary>
    /// 标记一次请求-响应过程，和响应对应
    /// </summary>
    public Guid InteractionId { get; set; }

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

public class ResponseViewItem : BaseViewModel, IResponseViewItem
{
    public ThemedIcon Icon
    {
        get { return Model?.Icon ?? Icons.APIIcon; }
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
    public int Latency { get; }

    public int Duration { get; }

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

    public ResponseViewItem(ILLMModel? model,IResponse response, string endPointName)
    {
        Duration = response.Duration;
        Model = model;
        Raw = response.Raw;
        Tokens = response.Tokens;
        IsInterrupt = response.IsInterrupt;
        ErrorMessage = response.ErrorMessage;
        EndPointName = endPointName;
        Latency = response.Latency;
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