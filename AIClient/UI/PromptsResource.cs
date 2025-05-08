namespace LLMClient.UI;

public interface IPromptsResource
{
    IReadOnlyList<string> SystemPrompts { get; }

    IReadOnlyList<string> UserPrompts { get; }

    Task Initialize();
}