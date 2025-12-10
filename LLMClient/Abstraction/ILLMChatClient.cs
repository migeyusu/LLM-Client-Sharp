using LLMClient.Endpoints;

namespace LLMClient.Abstraction;

public interface IChatEndpoint
{
    /// <summary>
    /// 唯一名称
    /// </summary>
    string Name { get; }
    
    bool IsResponding { get; set; }

    Task<CompletedResult> SendRequest(DialogContext context,
        IInvokeInteractor? interactor = null,
        CancellationToken cancellationToken = default);
}

public interface ILLMChatClient : IChatEndpoint
{
    ILLMAPIEndpoint Endpoint { get; }

    ILLMChatModel Model { get; }

    IModelParams Parameters { get; set; }
}