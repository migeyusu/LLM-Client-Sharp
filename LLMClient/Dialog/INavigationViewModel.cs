namespace LLMClient.Dialog;

public interface INavigationViewModel
{
    IDialogItem RootNode { get; }

    IDialogItem CurrentLeaf { get; set; }

    bool IsNodeSelectable(IDialogItem item);
}