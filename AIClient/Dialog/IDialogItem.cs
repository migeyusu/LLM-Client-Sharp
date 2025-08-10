using LLMClient.Abstraction;
using Microsoft.Extensions.AI;

namespace LLMClient.UI.Dialog;

public interface IDialogItem : ITokenizable
{
    IAsyncEnumerable<ChatMessage> GetMessagesAsync(CancellationToken cancellationToken);
    
    bool IsAvailableInContext { get; }
}