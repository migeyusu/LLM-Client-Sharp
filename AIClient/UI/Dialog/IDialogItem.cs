using Microsoft.Extensions.AI;

namespace LLMClient.UI.Dialog;

public interface IDialogItem : ITokenizable
{
    IAsyncEnumerable<ChatMessage> GetMessages(CancellationToken cancellationToken);
    
    bool IsAvailableInContext { get; }
}