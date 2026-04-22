using LLMClient.Dialog.Models;
using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

/// <summary>
/// 构建仅包含文本内容的对话上下文，用于向不支持 tool call 的模型发送摘要请求。
/// 会过滤掉 FunctionCallContent、FunctionResultContent、ReasoningContent 等非文本内容。
/// </summary>
public class TextOnlyDialogContextBuilder : DefaultRequestContextBuilder
{
    public TextOnlyDialogContextBuilder(IReadOnlyList<IChatHistoryItem> dialogItems) : base(dialogItems)
    {
    }

    public override async Task<List<ChatMessage>> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        var messages = await base.GetMessagesAsync(cancellationToken);
        var textOnlyMessages = new List<ChatMessage>(messages.Count);

        foreach (var message in messages)
        {
            var textContents = message.Contents.OfType<TextContent>().ToArray();
            if (textContents.Length == 0)
            {
                continue;
            }

            textOnlyMessages.Add(new ChatMessage(message.Role, textContents)
            {
                AdditionalProperties = message.AdditionalProperties,
                MessageId = message.MessageId,
                RawRepresentation = message.RawRepresentation,
                CreatedAt = message.CreatedAt,
                AuthorName = message.AuthorName,
            });
        }

        return textOnlyMessages;
    }
}