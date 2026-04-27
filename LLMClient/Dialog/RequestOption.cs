using LLMClient.Abstraction;
using LLMClient.Dialog.Models;

namespace LLMClient.Dialog;

public class RequestOption
{
    public required ILLMChatClient DefaultClient { get; init; }

    public required IRequestItem RequestItem { get; init; }

    public AgentDescriptor? Agent { get; set; }

    public bool UseAgent { get; set; }

    public required AgentConfig AgentConfig { get; init; }
}