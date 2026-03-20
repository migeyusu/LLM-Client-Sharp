using LLMClient.Abstraction;
using LLMClient.Agent.MiniSWE;
using LLMClient.Endpoints;

namespace LLMClient.Agent;

public interface IAgent
{
    IAsyncEnumerable<ChatCallResult> Execute(DialogContext context, IInvokeInteractor? interactor = null,
        CancellationToken cancellationToken = default);
}

/*public static class AgentExtensions
{
    public IAgent CreateAgent(string agentName)
    {
        switch (agentName)
        {
            case "mini-SWE":
                return new MiniSweAgent()
                break;
        }
        
    }
}*/