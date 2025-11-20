using System.Windows.Shell;
using LLMClient.Abstraction;
using LLMClient.Endpoints;
using LLMClient.ToolCall;
using LLMClient.UI.ViewModel.Base;
using Microsoft.Extensions.Logging;

namespace LLMClient.Research;

public abstract class ResearchClient : BaseViewModel, ILLMChatClient
{
    public abstract string Name { get; }

    public ILLMEndpoint Endpoint
    {
        get { return ProxyClient.Endpoint; }
    }

    public ILLMChatModel Model
    {
        get { return ProxyClient.Model; }
    }

    public bool IsResponding
    {
        get => ProxyClient.IsResponding;
        set => ProxyClient.IsResponding = value;
    }

    public IModelParams Parameters
    {
        get => ProxyClient.Parameters;
        set => ProxyClient.Parameters = value;
    }
    
    public ILLMChatClient ProxyClient { get; set; }

    protected ResearchClient(ILLMChatClient proxyClient)
    {
        ProxyClient = proxyClient;
    }

    public abstract Task<CompletedResult> SendRequest(DialogContext context, IInvokeInteractor? stream = null,
        CancellationToken cancellationToken = default);
    
    
}