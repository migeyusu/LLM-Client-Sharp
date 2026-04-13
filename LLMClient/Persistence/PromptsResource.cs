using LLMClient.Configuration;

namespace LLMClient.Persistence;

public interface IPromptsResource
{
    IReadOnlyList<PromptEntry> SystemPrompts { get; }

    Task Initialize();
}