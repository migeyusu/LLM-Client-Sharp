using System.Collections.ObjectModel;
using LLMClient.Dialog;
using LLMClient.Endpoints;
using LLMClient.MCP;

namespace LLMClient.Abstraction;

public interface ILLMClient
{
    /// <summary>
    /// 唯一名称
    /// </summary>
    string Name { get; }

    ILLMEndpoint Endpoint { get; }

    ILLMModel Model { get; }

    bool IsResponding { get; set; }

    IModelParams Parameters { get; set; }

    IFunctionInterceptor FunctionInterceptor { get; set; }

    ObservableCollection<string> RespondingText { get; }

    Task<CompletedResult> SendRequest(DialogContext context,
        CancellationToken cancellationToken = default);
}

public class DialogContext
{
    public DialogContext(IList<IDialogItem> dialogItems, string? systemPrompt = null)
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
}