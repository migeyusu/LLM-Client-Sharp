using LLMClient.Dialog;
using LLMClient.Dialog.Models;

namespace LLMClient.Abstraction;

public class DialogContext
{
    public DialogContext(IReadOnlyList<IDialogItem> history,
        string? systemPrompt = null)
    {
        DialogItems = history;
        SystemPrompt = systemPrompt;
        this.Request = DialogItems.LastOrDefault((item => item is RequestViewItem)) as RequestViewItem ??
                       throw new InvalidOperationException("RequestViewItem is null");
    }

    public string? SystemPrompt { get; }

    public IReadOnlyList<IDialogItem> DialogItems { get; }

    public RequestViewItem Request { get; }
}