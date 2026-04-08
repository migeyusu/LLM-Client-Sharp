using LLMClient.Configuration;

namespace LLMClient.Persistance;

public interface IPromptsResource
{
    IReadOnlyList<PromptEntry> SystemPrompts { get; }

    Task Initialize();
}