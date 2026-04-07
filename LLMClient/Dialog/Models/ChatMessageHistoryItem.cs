using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;

public sealed class ChatMessageHistoryItem : IChatHistoryItem
{
    private readonly ChatMessage _message;

    public ChatMessageHistoryItem(ChatMessage message)
    {
        _message = message;
    }

    public IEnumerable<ChatMessage> Messages
    {
        get
        {
            yield return _message;
        }
    }
}

