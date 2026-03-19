using LLMClient.Abstraction;

namespace LLMClient.Dialog.Models;

public interface IResponseItem : IInteractionItem
{
    //Task<IChatUsage> ProcessAsync(DialogContext context, CancellationToken token);
}