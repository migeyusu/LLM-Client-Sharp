using LLMClient.Abstraction;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Endpoints.Messages;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;

public class ResponseViewItemBase : BaseViewModel, IResponse
{
    public long Tokens
    {
        get => Usage?.OutputTokenCount ?? 0;
    }

    public int Latency
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public int Duration
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }


    public string? ErrorMessage
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public double? Price
    {
        get;
        set
        {
            if (Nullable.Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public UsageDetails? Usage
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Tokens));
        }
    }


    /// <summary>
    /// 是否中断
    /// </summary>
    public virtual bool IsInterrupt
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }


    /// <summary>
    /// response messages 来源于回复，但是为了前向兼容，允许基于raw生成
    /// </summary>
    public IList<ChatMessage>? ResponseMessages
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TextContent));
        }
    }

    public ChatFinishReason? FinishReason
    {
        get;
        set
        {
            if (Nullable.Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public IList<ChatAnnotation>? Annotations { get; set; }
}