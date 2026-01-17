using LLMClient.Abstraction;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog;

public interface IDialogItem : ITokenizable
{
    Guid Id { get; }

    IDialogItem? PreviousItem { get; }

    Guid? PreviousItemId { get; }

    public IReadOnlyCollection<IDialogItem> Children { get; }

    IAsyncEnumerable<ChatMessage> GetMessagesAsync(CancellationToken cancellationToken);

    bool IsAvailableInContext { get; }

    void AppendChild(IDialogItem child);
}