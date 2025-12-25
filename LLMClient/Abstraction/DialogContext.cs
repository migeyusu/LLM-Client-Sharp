using LLMClient.Dialog;

namespace LLMClient.Abstraction;

public class DialogContext
{
    public DialogContext(IList<IDialogItem> dialogItems,
        string? systemPrompt = null)
    {
        DialogItems = dialogItems;
        SystemPrompt = systemPrompt;
        if (DialogItems.Last() is RequestViewItem request)
        {
            Request = request;
        }
    }

    public string? SystemPrompt { get; }

    public IList<IDialogItem> DialogItems { get; }

    public RequestViewItem? Request { get; }

    public void Initialize(ITokensCounter counter)
    {
        foreach (var dialogItem in DialogItems)
        {
            if (dialogItem is RequestViewItem request)
            {
                request.CalculateTokensAsync(counter);
            }
        }
    }
}