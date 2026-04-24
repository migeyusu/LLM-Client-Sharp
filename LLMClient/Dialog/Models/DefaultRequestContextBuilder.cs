using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using LLMClient.Abstraction;
using LLMClient.Endpoints;
using LLMClient.Rag;
using LLMClient.ToolCall;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using AIContextProvider = Microsoft.Agents.AI.AIContextProvider;

namespace LLMClient.Dialog.Models;

/// <summary>
/// 用于隔离chatRequest和context
/// </summary>
public class DefaultRequestContextBuilder
{
    protected DefaultRequestContextBuilder(IReadOnlyList<IChatHistoryItem> dialogItems)
    {
        ChatHistoryItems = dialogItems;
    }

    public static DefaultRequestContextBuilder CreateFromSession(ITextDialogSession session)
    {
        var workingResponse = session.WorkingResponse;
        var historyItems = session.GetChatHistory().ToArray();
        return CreateFromHistory(historyItems, workingResponse.Id, session.ContextProviders,
            session.SystemPrompt, session.ID, session.WorkingDirectory);
    }

    public static DefaultRequestContextBuilder CreateFromHistory(IReadOnlyList<IChatHistoryItem> history,
        Guid? dialogId = null, AIContextProvider[]? contextProviders = null, string? systemPrompt = null,
        Guid? sessionId = null, string? workingDirectory = null)
    {
        var requestViewItem = history.LastOrDefault() as IRequestItem ??
                              throw new InvalidOperationException("RequestViewItem is null");
        var dialogContext = new DefaultRequestContextBuilder(history)
        {
            SystemPrompt = systemPrompt,
            SessionId = sessionId,
            DialogId = dialogId ?? Guid.NewGuid(),
            ContextProviders = contextProviders,
            WorkingDirectory = workingDirectory,
        };
        dialogContext.MapFromRequest(requestViewItem);
        return dialogContext;
    }

    /// <summary>
    /// message don't contains system prompt
    /// </summary>
    public virtual async Task<List<ChatMessage>> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        var chatMessages = new List<ChatMessage>();
        foreach (var chatHistoryItem in ChatHistoryItems)
        {
            if (chatHistoryItem is RequestViewItem requestViewItem)
            {
                await requestViewItem.EnsureDataAsync(cancellationToken);
            }

            var messages = chatHistoryItem.Messages;
            foreach (var message in messages)
            {
                // Level 3: DialogItem tag
                if (chatHistoryItem is IDialogItem dialogItem)
                {
                    message.TagDialogLevel(dialogItem);
                }

                // Level 2: Interaction tag
                if (chatHistoryItem is IInteractionItem interactionItem)
                {
                    message.TagInteractionLevel(interactionItem);
                }

                // Level 1: Session tag
                if (SessionId.HasValue)
                {
                    message.TagSessionLevel(SessionId.Value);
                }

                chatMessages.Add(message);
            }
        }

        return chatMessages;
    }

    public void MapFromRequest(IRequestConfig config)
    {
        RequesterViewModel.IChatRequestMapper.Map<IRequestConfig, DefaultRequestContextBuilder>(config, this);
        this.FunctionGroups = config.FunctionGroups?.ToList();
    }

    public string? UserPrompt { get; set; }

    public IReadOnlyList<IChatHistoryItem> ChatHistoryItems { get; }

    public string? WorkingDirectory { get; set; }

    public string? SystemPrompt { get; set; }

    public Guid? SessionId { get; set; }

    public required Guid DialogId { get; init; }

    public ISearchOption? SearchOption { get; set; }

    public required AIContextProvider[]? ContextProviders { get; init; }

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
            DialogId = this.DialogId.ToString(),
            ChatMessages = chatHistory,
            FunctionCallEngine = functionCallEngine,
            RequestOptions = requestOptions,
            TempAdditionalProperties = TempAdditionalProperties,
            AutoApproveAllInvocations = AutoApproveAllInvocations,
            ShowRequestJson = this.IsDebugMode,
            ContextProviders = this.ContextProviders,
            WorkingDirectory = WorkingDirectory,
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
            ChatMessage systemMessage;
            if (model.SupportSystemPrompt)
            {
                systemMessage = new ChatMessage(ChatRole.System, systemPrompt);
            }
            else
            {
                Trace.TraceWarning(
                    "System prompt is not supported by this model, but system prompt or additional prompt is provided. The prompt will be added as the first message in the chat history.");
                systemMessage = new ChatMessage(ChatRole.User, systemPrompt);
            }

            // System message gets session tag but not dialog/interaction tags
            if (SessionId.HasValue)
            {
                systemMessage.TagSessionLevel(SessionId.Value);
            }

            chatHistory.Add(systemMessage);
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

        builder.AppendLine(
            "For the following tool groups, you can call them by name with the required parameters:");
        foreach (var functionGroup in availableGroups)
        {
            if (string.IsNullOrWhiteSpace(functionGroup.AdditionPrompt))
            {
                builder.AppendLine("Tools start with " + functionGroup.Name);
            }
            else
            {
                builder.AppendLine($"Tools start with {functionGroup.Name}:{functionGroup.AdditionPrompt}");
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