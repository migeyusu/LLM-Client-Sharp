namespace LLMClient.Dialog;

public interface IEditableDialogItem : IDialogItem
{
    void TriggerTextContentUpdate();
}