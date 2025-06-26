using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.UI.Component;
using Microsoft.Extensions.AI;

namespace LLMClient.UI.Dialog;

public class ResponseViewItem : BaseViewModel, IResponseViewItem
{
    public ThemedIcon Icon
    {
        get { return Model?.Icon ?? ImageExtensions.APIIcon; }
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

    public double? Price { get; set; }

    private SearchableDocument? _flowDocument = null;

    public SearchableDocument? Document
    {
        get
        {
            if (string.IsNullOrEmpty(this.Raw))
            {
                return null;
            }

            if (_flowDocument == null)
            {
                _flowDocument = new SearchableDocument(this.Raw);
            }

            return _flowDocument;
        }
    }

    public string? Raw { get; }

    public string? ResponseWithoutThinking { get; }

    private const string ThinkingTag = "</think>";

    public ResponseViewItem(ILLMModel? model, IResponse response, string endPointName)
    {
        Duration = response.Duration;
        Model = model;
        Raw = response.Raw;
        if (Raw != null)
        {
            var index = Raw.IndexOf(ThinkingTag, StringComparison.Ordinal);
            ResponseWithoutThinking = index > 0 ? Raw[(index + ThinkingTag.Length)..].Trim() : Raw;
        }

        Tokens = response.Tokens;
        IsInterrupt = response.IsInterrupt;
        ErrorMessage = response.ErrorMessage;
        EndPointName = endPointName;
        Latency = response.Latency;
        Price = response.Price;
    }

    private ChatMessage? _assistantMessage;

    public Task<ChatMessage?> GetMessage()
    {
        if (Raw == null)
        {
            return Task.FromResult<ChatMessage?>(null);
        }

        if (_assistantMessage == null)
        {
            _assistantMessage = new ChatMessage(ChatRole.Assistant, Raw);
        }

        return Task.FromResult<ChatMessage?>(_assistantMessage);
    }


    public bool IsAvailableInContext
    {
        get { return !IsInterrupt; }
    }
}