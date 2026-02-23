namespace LLMClient.Dialog.Models;

public interface IEditableDialogItem : IDialogItem
{
    void TriggerTextContentUpdate();
}