using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;

public interface IChatHistoryItem
{
    /// <summary>
    /// raw messages
    /// </summary>
    IEnumerable<ChatMessage> Messages { get; }

    /// <summary>
    /// optimized for context
    /// </summary>
    /// <returns></returns>
    IEnumerable<ChatMessage> GetMessagesForContext()
    {
        return Messages;
    }
}