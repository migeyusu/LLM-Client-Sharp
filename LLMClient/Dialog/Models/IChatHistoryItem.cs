using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;

public interface IChatHistoryItem
{
    IEnumerable<ChatMessage> Messages { get; }
}