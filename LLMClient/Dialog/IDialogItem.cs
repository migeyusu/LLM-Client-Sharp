using LLMClient.Abstraction;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog;

public interface IDialogItem : ITokenizable
{
    Guid Id { get; set; }

    ChatRole Role { get; }

    IDialogItem? PreviousItem { get; }

    Guid? PreviousItemId => PreviousItem?.Id;

    IReadOnlyCollection<IDialogItem> Children { get; }

    IAsyncEnumerable<ChatMessage> GetMessagesAsync(CancellationToken cancellationToken);

    bool IsAvailableInContext { get; }

    IDialogItem AppendChild(IDialogItem child);

    IDialogItem RemoveChild(IDialogItem child);

    void ClearChildren();
}