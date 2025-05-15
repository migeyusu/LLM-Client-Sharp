namespace LLMClient.Data;

public interface IPromptsResource
{
    IReadOnlyList<string> SystemPrompts { get; }

    IReadOnlyList<string> UserPrompts { get; }

    Task Initialize();
}