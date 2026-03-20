using LLMClient.Abstraction;

namespace LLMClient.Dialog.Models;

public interface IRequestItem : IEditableDialogItem, IInteractionItem, IChatRequest
{
    
}