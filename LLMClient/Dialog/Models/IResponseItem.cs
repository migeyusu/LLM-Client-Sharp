using LLMClient.Abstraction;

namespace LLMClient.Dialog.Models;

public interface IResponseItem : IDialogItem, IInteractionItem
{
    //Task<IChatUsage> ProcessAsync(DialogContext context, CancellationToken token);
    bool IsResponding { get; }
}