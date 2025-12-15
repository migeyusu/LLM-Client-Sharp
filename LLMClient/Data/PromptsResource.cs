using LLMClient.Configuration;

namespace LLMClient.Data;

public interface IPromptsResource
{
    IReadOnlyList<PromptEntry> SystemPrompts { get; }

    Task Initialize();
}