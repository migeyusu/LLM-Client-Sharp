using LLMClient.Abstraction;

namespace LLMClient.Agent;

/// <summary>
/// Marks an agent that runs against a single chat client model.
/// </summary>
public interface ISingleClientAgent : IAgent
{
    ILLMChatClient ChatClient { get; }
}

