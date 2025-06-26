using Microsoft.Extensions.AI;

namespace LLMClient.UI.Dialog;

public interface IDialogItem : ITokenizable
{
    Task<ChatMessage?> GetMessage();

    bool IsAvailableInContext { get; }
}