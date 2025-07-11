using System.Collections.ObjectModel;
using LLMClient.Abstraction;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI.Dialog;

namespace LLMClient.Endpoints;

public class NullLlmModelClient : ILLMClient
{
    public static NullLlmModelClient Instance => new NullLlmModelClient();

    public string Name { get; } = "NullLlmModelClient";

    public ILLMEndpoint Endpoint
    {
        get { return new APIEndPoint(); }
    }

    public ILLMModel Model
    {
        get { return new APIModelInfo(); }
    }

    public bool IsResponding { get; set; } = false;
    public IModelParams Parameters { get; set; } = new DefaultModelParam();
    public ObservableCollection<string> RespondingText { get; } = new ObservableCollection<string>();

    public Task<CompletedResult> SendRequest(IList<IDialogItem> dialogItems,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("This client does not support sending requests.");
    }
}