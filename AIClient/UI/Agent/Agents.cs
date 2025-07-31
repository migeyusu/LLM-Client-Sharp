using System.Runtime.CompilerServices;
using LLMClient.UI.Dialog;
using Microsoft.Extensions.AI;

namespace LLMClient.UI.Agent;

public static class Agents
{
    public static PromptAgent SchedularAgent { get; } = new PromptAgent
    {
        InteractionId = Guid.NewGuid(),
        Prompt = "You are a helpful AI assistant. Please assist the user with their queries."
    };
}

public interface IAgent
{
}

public abstract class AgentBase : IAgent, IRequestItem
{
    public virtual long Tokens { get; } = 0;

    public abstract IAsyncEnumerable<ChatMessage> GetMessages(CancellationToken cancellationToken);

    public bool IsAvailableInContext { get; } = true;
    public Guid InteractionId { get; set; }
}

public class PromptAgent : AgentBase
{
    public string Prompt { get; set; } = string.Empty;

    public override async IAsyncEnumerable<ChatMessage> GetMessages([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(Prompt))
        {
            throw new NotSupportedException("Prompt cannot be null or empty.");
        }

        yield return new ChatMessage(ChatRole.User, Prompt);
    }
}