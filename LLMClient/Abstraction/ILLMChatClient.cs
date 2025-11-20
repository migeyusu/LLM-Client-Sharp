using LLMClient.Endpoints;
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

    Task<CompletedResult> SendRequest(DialogContext context,
        IInvokeInteractor? interactor = null,
        CancellationToken cancellationToken = default);
}