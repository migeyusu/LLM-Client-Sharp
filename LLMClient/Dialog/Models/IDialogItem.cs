using LLMClient.Abstraction;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;

public interface IDialogItem : IChatHistoryItem, ITokenizable
{
    Guid Id { get; set; }

    ChatRole Role { get; }

    IDialogItem? PreviousItem { get; }

    Guid? PreviousItemId => PreviousItem?.Id;

    IReadOnlyCollection<IDialogItem> Children { get; }

    bool HasFork { get; }

    bool IsAvailableInContext { get; }

    IDialogItem AppendChild(IDialogItem child);

    IDialogItem RemoveChild(IDialogItem child);

    void ClearChildren();
}