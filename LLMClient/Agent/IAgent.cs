using LLMClient.Abstraction;
using LLMClient.Endpoints;

namespace LLMClient.Agent;

public interface IAgent
{
    IAsyncEnumerable<ChatCallResult> Execute(DialogContext context, IInvokeInteractor? interactor = null,
        CancellationToken cancellationToken = default);
}