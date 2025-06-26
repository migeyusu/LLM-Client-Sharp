using System.Collections.ObjectModel;
using LLMClient.Abstraction;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI.Dialog;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;

namespace LLMClient.Endpoints;

public class NullLlmModelClient : ILLMClient
{
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

    public Task<CompletedResult> SendRequest(IEnumerable<IDialogItem> dialogItems,
        IList<IAIFunctionGroup>? functions = null, CancellationToken cancellationToken = bad)
    {
        throw new NotSupportedException("This client does not support sending requests.");
    }
}