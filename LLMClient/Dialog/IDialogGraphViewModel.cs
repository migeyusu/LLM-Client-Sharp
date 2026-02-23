using LLMClient.Dialog.Models;

namespace LLMClient.Dialog;

public interface IDialogGraphViewModel
{
    IDialogItem RootNode { get; }

    IDialogItem CurrentLeaf { get; set; }

    bool IsNodeSelectable(IDialogItem item);

    void DeleteItem(IDialogItem item);
}