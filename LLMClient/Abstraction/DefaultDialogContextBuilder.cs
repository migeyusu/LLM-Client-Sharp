using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using AutoMapper;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.Rag;
using LLMClient.ToolCall;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;

namespace LLMClient.Abstraction;

/// <summary>
/// 用于隔离chatrequest和context
/// </summary>
public class DefaultDialogContextBuilder : IChatRequest
{
    private static readonly Lazy<IMapper> MapperLazy = new(() =>
    {
        var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<IChatRequest, DefaultDialogContextBuilder>();
                cfg.CreateMap<IChatRequest, RequestViewItem>();
            },
            NullLoggerFactory.Instance);
        return config.CreateMapper();
    });

    public static IMapper IChatRequestMapper => MapperLazy.Value;

    public DefaultDialogContextBuilder(IReadOnlyList<IChatHistoryItem> dialogItems)
    {
        ChatHistoryItems = dialogItems;
    }

    public static DefaultDialogContextBuilder CreateFromResponse(IResponseItem response, string? systemPrompt = null)
    {
        var history = response.GetChatHistory().ToArray();
        return CreateFromHistory(history, systemPrompt);
    }

    public static DefaultDialogContextBuilder CreateFromSession(ITextDialogSession session)
    {
        var historyItems = session.GetHistory();
        var systemPrompt = session.SystemPrompt;
        return CreateFromHistory(historyItems, systemPrompt);
    }

    public static DefaultDialogContextBuilder CreateFromHistory(IReadOnlyList<IChatHistoryItem> history,
        string? systemPrompt = null)
    {
        var requestViewItem = history.LastOrDefault() as IRequestItem ??
                              throw new InvalidOperationException("RequestViewItem is null");
        var dialogContext = new DefaultDialogContextBuilder(history)
        {
            SystemPrompt = systemPrompt
        };
        dialogContext.MapFromRequest(requestViewItem);
        return dialogContext;
    }

    public void MapFromRequest(IChatRequest context)
    {
        MapperLazy.Value.Map(context, this);
    }

    public string? UserPrompt { get; set; }

    /// <summary>
    /// message don't contains system prompt
    /// </summary>
    public virtual async Task<List<ChatMessage>> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        var chatMessages = new List<ChatMessage>();
        foreach (var dialogItem in ChatHistoryItems)
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

    public IReadOnlyList<IChatHistoryItem> ChatHistoryItems { get; }

    public string? WorkingDirectory { get; set; }

    public string? SystemPrompt { get; set; }

    public ISearchOption? SearchOption { get; set; }

    public List<CheckableFunctionGroupTree>? FunctionGroups { get; set; }

    public IRagSource[]? RagSources { get; set; }

    public ChatResponseFormat? ResponseFormat { get; set; }

    public FunctionCallEngineType CallEngineType { get; set; }

    public FunctionCallEngine? CallEngine { get; set; }

    public AdditionalPropertiesDictionary? TempAdditionalProperties { get; set; }

    public bool IsDebugMode { get; set; }

    public bool AutoApproveAllInvocations { get; set; }

    [MemberNotNull(nameof(CallEngine))]
    protected FunctionCallEngine EnsureCallEngine(bool supportFunctionCall)
    {
        if (CallEngine == null)
        {
            //如果不原生支持函数调用，切换到prompt实现
            CallEngine =
                FunctionCallEngine.Create(supportFunctionCall ? CallEngineType : FunctionCallEngineType.Prompt);
        }

        return CallEngine;
    }

    public virtual async Task<RequestContext> BuildAsync(
        IEndpointModel model,
        CancellationToken cancellationToken = default)
    {
        await ApplySearchAsync(cancellationToken);

        var functionCallEngine = EnsureCallEngine(model.SupportFunctionCall);
        var kernelPluginCollection = functionCallEngine.KernelPluginCollection;

        await RegisterFunctionGroupsAsync(kernelPluginCollection, cancellationToken);
        await RegisterRagSourcesAsync(kernelPluginCollection, cancellationToken);

        var chatHistory = await BuildChatHistoryAsync(model, functionCallEngine, cancellationToken);
        var requestOptions = BuildRequestOptions(model, functionCallEngine, chatHistory);

        return new RequestContext
        {
            ChatMessages = chatHistory,
            FunctionCallEngine = functionCallEngine,
            RequestOptions = requestOptions,
            TempAdditionalProperties = TempAdditionalProperties,
            AutoApproveAllInvocations = AutoApproveAllInvocations,
            ShowRequestJson = this.IsDebugMode
        };
    }

    protected virtual async Task ApplySearchAsync(CancellationToken cancellationToken)
    {
        if (SearchOption != null)
        {
            await SearchOption.ApplySearch(this);
        }
    }

    protected virtual async Task RegisterFunctionGroupsAsync(
        KernelPluginCollection kernelPluginCollection,
        CancellationToken cancellationToken)
    {
        if (FunctionGroups == null)
        {
            return;
        }

        foreach (var functionGroup in FunctionGroups)
        {
            await functionGroup.EnsureAsync(cancellationToken);
            if (!functionGroup.IsAvailable)
            {
                continue;
            }

            var availableTools = functionGroup.AvailableTools;
            if (availableTools == null || availableTools.Count == 0)
            {
                continue;
            }

            kernelPluginCollection.AddFromFunctions(
                functionGroup.Name,
                availableTools.Select(function => function.AsKernelFunction()));
        }
    }

    protected virtual async Task RegisterRagSourcesAsync(
        KernelPluginCollection kernelPluginCollection,
        CancellationToken cancellationToken)
    {
        if (RagSources == null || RagSources.Length == 0)
        {
            return;
        }

        var resourceIndex = 0;
        foreach (var ragSource in RagSources)
        {
            await ragSource.EnsureAsync(cancellationToken);
            if (!ragSource.IsAvailable)
            {
                continue;
            }

            if (ragSource is RagFileBase ragFile)
            {
                ragFile.FileIndexInContext = resourceIndex;
                resourceIndex++;
            }

            var availableTools = ragSource.AvailableTools;
            if (availableTools == null || availableTools.Count == 0)
            {
                continue;
            }

            kernelPluginCollection.AddFromFunctions(
                ragSource.Name,
                availableTools.Select(function => function.AsKernelFunction()));
        }
    }

    protected virtual async Task<List<ChatMessage>> BuildChatHistoryAsync(
        IEndpointModel model,
        FunctionCallEngine functionCallEngine,
        CancellationToken cancellationToken)
    {
        var chatHistory = new List<ChatMessage>();
        var chatMessages = await GetMessagesAsync(cancellationToken);
        var systemPrompt = BuildSystemPrompt();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            if (model.SupportSystemPrompt)
            {
                chatHistory.Add(new ChatMessage(ChatRole.System, systemPrompt));
            }
            else
            {
                Trace.TraceWarning(
                    "System prompt is not supported by this model, but system prompt or additional prompt is provided. The prompt will be added as the first message in the chat history.");
                chatHistory.Add(new ChatMessage(ChatRole.User, systemPrompt));
            }
        }

        chatHistory.AddRange(chatMessages);
        return chatHistory;
    }

    protected virtual string? BuildSystemPrompt()
    {
        var systemPrompt = SystemPrompt;
        var additionalPromptBuilder = new StringBuilder();

        AppendFunctionGroupsPrompt(additionalPromptBuilder);
        AppendRagSourcesPrompt(additionalPromptBuilder);

        var additionalPrompt = additionalPromptBuilder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(additionalPrompt))
        {
            return systemPrompt;
        }

        return string.IsNullOrWhiteSpace(systemPrompt)
            ? additionalPrompt
            : $"{systemPrompt}\n\n{additionalPrompt}";
    }

    protected virtual void AppendFunctionGroupsPrompt(StringBuilder builder)
    {
        if (FunctionGroups == null)
        {
            return;
        }

        var availableGroups = FunctionGroups
            .Where(group => group.IsAvailable)
            .Where(group => group.AvailableTools is { Count: > 0 })
            .ToList();

        if (availableGroups.Count == 0)
        {
            return;
        }

        builder.AppendLine("For the following functions, you can call them by name with the required parameters:");
        foreach (var functionGroup in availableGroups)
        {
            if (string.IsNullOrWhiteSpace(functionGroup.AdditionPrompt))
            {
                builder.AppendLine(functionGroup.Name);
            }
            else
            {
                builder.AppendLine($"{functionGroup.Name}:{functionGroup.AdditionPrompt}");
            }
        }
    }

    protected virtual void AppendRagSourcesPrompt(StringBuilder builder)
    {
        if (RagSources == null)
        {
            return;
        }

        var availableSources = RagSources
            .Where(source => source.IsAvailable)
            .Where(source => source.AvailableTools is { Count: > 0 })
            .ToList();

        if (availableSources.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine(
            "For the following RAG(Retrieval-Augmented Generation) sources such as files, web contents, you can get information by call them with the required parameters:");
        foreach (var ragSource in availableSources)
        {
            if (string.IsNullOrWhiteSpace(ragSource.AdditionPrompt))
            {
                builder.AppendLine(ragSource.Name);
            }
            else
            {
                builder.AppendLine($"{ragSource.Name}:{ragSource.AdditionPrompt}");
            }
        }
    }

    protected virtual ChatOptions BuildRequestOptions(
        IEndpointModel model,
        FunctionCallEngine functionCallEngine,
        List<ChatMessage> chatHistory)
    {
        var requestOptions = new ChatOptions
        {
            ResponseFormat = ResponseFormat
        };
        if (functionCallEngine.KernelPluginCollection.Count > 0)
        {
            functionCallEngine.PreviewRequest(requestOptions, model, chatHistory);
        }

        return requestOptions;
    }
}