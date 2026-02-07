using LLMClient.Dialog;

namespace LLMClient.Abstraction;

public class DialogContext
{
    public DialogContext(IReadOnlyList<IDialogItem> history,
        string? systemPrompt = null)
    {
        DialogItems = history;
        SystemPrompt = systemPrompt;
        if (DialogItems.Last() is RequestViewItem request)
        {
            Request = request;
        }
    }

    public string? SystemPrompt { get; }

    public IReadOnlyList<IDialogItem> DialogItems { get; }

    public RequestViewItem? Request { get; }
}