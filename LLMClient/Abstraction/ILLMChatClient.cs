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
    /// <exception cref="OutOfContextWindowException">超过LLM窗口大小，不需要重试</exception>
    /// <param name="context"></param>
    /// <param name="interactor"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<CompletedResult> SendRequest(DialogContext context,
        IInvokeInteractor? interactor = null,
        CancellationToken cancellationToken = default);
}

public interface IParameterizedLLMModel
{
    ILLMModel Model { get; }

    IModelParams Parameters { get; set; }
}

public interface ILLMChatClient : IParameterizedLLMModel, IChatEndpoint
{
    ILLMAPIEndpoint Endpoint { get; }
}