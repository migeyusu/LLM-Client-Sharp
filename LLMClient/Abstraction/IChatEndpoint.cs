using LLMClient.Endpoints;

namespace LLMClient.Abstraction;

public interface IChatEndpoint
{
    /// <summary>
    /// 唯一名称
    /// </summary>
    string Name { get; }

    bool IsResponding { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="interactor"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ChatCallResult> SendRequest(DialogContext context,
        IInvokeInteractor? interactor = null,
        CancellationToken cancellationToken = default);
}