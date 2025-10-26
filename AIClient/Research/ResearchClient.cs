﻿using LLMClient.Abstraction;
using LLMClient.Endpoints;
using LLMClient.MCP;
using LLMClient.UI;
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

    public IFunctionInterceptor FunctionInterceptor { get; set; } = FunctionAuthorizationInterceptor.Instance;

    public ILLMChatClient ProxyClient { get; set; }

    protected ResearchClient(ILLMChatClient proxyClient)
    {
        ProxyClient = proxyClient;
    }

    public abstract Task<CompletedResult> SendRequest(DialogContext context, Action<string>? stream = null,
        ILogger? logger = null,
        CancellationToken cancellationToken = default);
    
    
}