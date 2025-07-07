using System.Collections.ObjectModel;
using LLMClient.Endpoints;
using LLMClient.UI.Dialog;

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

    ObservableCollection<string> RespondingText { get; }

    Task<CompletedResult> SendRequest(IList<IDialogItem> dialogItems, string? systemPrompt = null,
        CancellationToken cancellationToken = default);
}