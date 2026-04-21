using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace LLMClient.Endpoints;

public class AgentFallbackClient : IChatClient
{
    private readonly ChatClientAgent _agent;

    public AgentFallbackClient(ChatClientAgent agent)
    {
        this._agent = agent;
    }

    public void Dispose()
    {
    }

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var chatClientAgentRunOptions = new ChatClientAgentRunOptions(options);
        var agentResponse = await _agent.RunAsync(messages, cancellationToken: cancellationToken,
            options: chatClientAgentRunOptions);
        return (ChatResponse)agentResponse.RawRepresentation!;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var agentResponseUpdate in _agent.RunStreamingAsync(messages,
                           cancellationToken: cancellationToken,
                           options: new ChatClientAgentRunOptions(options)))
        {
            yield return (ChatResponseUpdate)agentResponseUpdate.RawRepresentation!;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return _agent.GetService(serviceType, serviceKey);
    }
}