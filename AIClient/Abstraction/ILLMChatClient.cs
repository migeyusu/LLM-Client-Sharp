﻿using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using LLMClient.Endpoints;
using LLMClient.MCP;
using Microsoft.Extensions.Logging;

namespace LLMClient.Abstraction;

public interface ILLMChatClient
{
    /// <summary>
    /// 唯一名称
    /// </summary>
    string Name { get; }

    ILLMEndpoint Endpoint { get; }

    ILLMChatModel Model { get; }

    bool IsResponding { get; set; }

    IModelParams Parameters { get; set; }

    IFunctionInterceptor FunctionInterceptor { get; set; }

    Task<CompletedResult> SendRequest(DialogContext context,
        Action<string>? stream = null,
        ILogger? logger = null,
        CancellationToken cancellationToken = default);
}