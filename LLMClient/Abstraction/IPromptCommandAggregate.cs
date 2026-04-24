namespace LLMClient.Abstraction;

public interface IPromptCommandAggregate
{
    string[]? AvailableCommands { get; }

    Task<string> TryGetInjectedPromptAsync(string userInput, CancellationToken ct = default);
}