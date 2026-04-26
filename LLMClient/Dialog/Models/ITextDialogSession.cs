﻿﻿﻿using LLMClient.Abstraction;
using LLMClient.Agent.MiniSWE;
using LLMClient.ToolCall;
using Microsoft.Agents.AI;

namespace LLMClient.Dialog.Models;

public interface ITextDialogSession
{
    /// <summary>
    /// session id
    /// </summary>
    Guid ID { get; }

    IReadOnlyList<IDialogItem> VisualDialogItems { get; }

    IResponseItem WorkingResponse { get; }

    IEnumerable<IDialogItem> GetChatHistory()
    {
        return WorkingResponse.GetChatHistory();
    }

    Task CutContextAsync(IRequestItem? requestItem = null);

    AIContextProvider[]? ContextProviders { get; }

    IPromptCommandAggregate? PromptCommand { get; }

    string? SystemPrompt { get; }

    IEnumerable<Type> SupportedAgents { get; }

    IFunctionGroupSource? ToolsSource { get; }

    Task<IResponse> NewResponse(RequestOption option,
        IRequestItem? insertBefore = null, CancellationToken token = default);
}

/// <summary>
/// Project-specific session interface extending ITextDialogSession
/// with working directory and platform information.
/// </summary>
public interface IProjectSession : ITextDialogSession
{
    string? WorkingDirectory { get; }

    RunPlatform Platform { get; }
}