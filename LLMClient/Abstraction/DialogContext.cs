using System.Diagnostics.CodeAnalysis;
using AutoMapper;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace LLMClient.Abstraction;

public class DialogContext : IChatRequest
{
    private static Lazy<IMapper> _mapperLazy = new(() =>
    {
        var config = new MapperConfiguration(cfg => cfg.CreateMap<IChatRequest, DialogContext>(),
            NullLoggerFactory.Instance);
        return config.CreateMapper();
    });

    public DialogContext(IReadOnlyList<IChatHistoryItem> dialogItems)
    {
        DialogItems = dialogItems;
    }

    public static DialogContext CreateFromResponse(IResponseItem response, string? systemPrompt = null)
    {
        var history = response.GetChatHistory().ToArray();
        return CreateFromHistory(history, systemPrompt);
    }

    public static DialogContext CreateFromHistory(IReadOnlyList<IChatHistoryItem> history,
        string? systemPrompt = null)
    {
        var requestViewItem = history.LastOrDefault() as RequestViewItem ??
                              throw new InvalidOperationException("RequestViewItem is null");
        var dialogContext = new DialogContext(history)
        {
            SystemPrompt = systemPrompt
        };
        dialogContext.MapFromRequest(requestViewItem);
        return dialogContext;
    }

    public void MapFromRequest(IChatRequest context)
    {
        _mapperLazy.Value.Map(context, this);
    }

    public string? UserPrompt
    {
        get
        {
            var requestViewItem = DialogItems.OfType<RequestViewItem>().LastOrDefault();
            return requestViewItem?.RawTextMessage;
        }
    }

    /// <summary>
    /// message don't contains system prompt
    /// </summary>
    public async Task<List<ChatMessage>> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        var chatMessages = new List<ChatMessage>();
        foreach (var dialogItem in DialogItems)
        {
            if (dialogItem is RequestViewItem requestViewItem)
            {
                await requestViewItem.EnsureInitializeAsync(cancellationToken);
            }

            var messages = dialogItem.Messages;
            chatMessages.AddRange(messages);
        }

        return chatMessages;
    }

    public IReadOnlyList<IChatHistoryItem> DialogItems { get; }

    public string? WorkingDirectory { get; set; }

    public string? SystemPrompt { get; set; }

    public ISearchOption? SearchOption { get; set; }

    public List<IAIFunctionGroup>? FunctionGroups { get; set; }

    public IRagSource[]? RagSources { get; set; }

    public ChatResponseFormat? ResponseFormat { get; set; }

    public FunctionCallEngineType CallEngineType { get; set; }

    public FunctionCallEngine? CallEngine { get; set; }

    public AdditionalPropertiesDictionary? TempAdditionalProperties { get; set; }

    public bool IsDebugMode { get; set; }

    [MemberNotNull(nameof(CallEngine))]
    public FunctionCallEngine EnsureCallEngine(bool supportFunctionCall)
    {
        if (CallEngine == null)
        {
            //如果不原生支持函数调用，切换到prompt实现
            this.CallEngine =
                FunctionCallEngine.Create(supportFunctionCall ? this.CallEngineType : FunctionCallEngineType.Prompt);
        }

        return CallEngine;
    }
}