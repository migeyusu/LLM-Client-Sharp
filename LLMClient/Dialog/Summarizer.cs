using System.Diagnostics;
using System.Windows;
using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Configuration;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog;

public class Summarizer(GlobalOptions options)
{
    public Task<string?> SummarizeSessionTopicAsync(ITextDialogSession dialog, Duration duration)
    {
        var dialogItems = new List<IDialogItem>(dialog.DialogItems.Take(3))
        {
            new RequestViewItem(options.SubjectSummarizePrompt)
        };

        return ExecuteSummarizeAsync(
            options.CreateSubjectSummarizeClient(),
            dialogItems,
            duration,
            "生成话题摘要失败");
    }

    public Task<string?> SummarizeSessionConversationHistoryAsync(ITextDialogSession dialog, Duration duration)
    {
        var dialogItems = new List<IDialogItem>(dialog.DialogItems)
        {
            new RequestViewItem(options.ConversationHistorySummaryPrompt)
        };

        return ExecuteSummarizeAsync(
            options.CreateContextSummarizeClient(),
            dialogItems,
            duration,
            "生成会话历史总结失败");
    }

    public Task<string?> SummarizeChatMessagesAsync(
        IReadOnlyList<ChatMessage> chatMessages,
        string prompt,
        Duration duration,
        ILLMChatClient? fallbackClient = null,
        CancellationToken cancellationToken = default)
    {
        var dialogItems = new List<IChatHistoryItem>(chatMessages.Count + 1);
        var messageHistoryItems = chatMessages.Select(message => new ChatMessageHistoryItem(message)).ToArray();
        dialogItems.AddRange(messageHistoryItems);
        dialogItems.Add(CreateRequest(prompt));

        return ExecuteSummarizeAsync(
            options.CreateContextSummarizeClient() ?? fallbackClient,
            dialogItems,
            duration,
            "生成上下文压缩摘要失败",
            cancellationToken);
    }

    private static async Task<string?> ExecuteSummarizeAsync(
        ILLMChatClient? client,
        IReadOnlyList<IChatHistoryItem> chatHistoryItems,
        Duration duration,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        if (client == null)
        {
            return null;
        }

        try
        {
            var dialogContext = new DefaultDialogContextBuilder(chatHistoryItems);
            var response = await new PromptBasedAgent(client)
                {
                    Timeout = duration,
                }
                .SendRequestAsync(dialogContext, cancellationToken);
            return response.FirstTextResponse;
        }
        catch (Exception e)
        {
            Trace.TraceError($"{errorMessage}：{e}");
            return null;
        }
    }

    public string ConversationHistorySummaryPrompt => options.ConversationHistorySummaryPrompt;

    private static RequestViewItem CreateRequest(string prompt)
    {
        return new RequestViewItem(prompt)
        {
            InteractionId = Guid.NewGuid(),
            CallEngineType = FunctionCallEngineType.Prompt,
            IsDebugMode = true,
        };
    }

    // Moved to GlobalOptions.cs as ConversationHistorySummaryPromptString
}