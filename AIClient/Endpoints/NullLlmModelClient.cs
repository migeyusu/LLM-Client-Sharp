using System.Collections.ObjectModel;
using LLMClient.Abstraction;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.MCP;

namespace LLMClient.Endpoints;

public class NullLlmModelClient : ILLMChatClient
{
    public static NullLlmModelClient Instance => new NullLlmModelClient();

    public string Name { get; } = "NullLlmModelClient";

    public ILLMEndpoint Endpoint
    {
        get { return new APIEndPoint(); }
    }

    public ILLMChatModel Model
    {
        get { return new APIModelInfo(); }
    }

    public bool IsResponding { get; set; } = false;
    public IModelParams Parameters { get; set; } = new DefaultModelParam();
    public IFunctionInterceptor FunctionInterceptor { get; set; } = FunctionAuthorizationInterceptor.Instance;
    public ObservableCollection<string> RespondingText { get; } = new ObservableCollection<string>();

    public Task<CompletedResult> SendRequest(DialogContext context,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("This client does not support sending requests.");
    }
}