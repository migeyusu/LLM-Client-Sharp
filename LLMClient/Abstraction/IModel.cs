namespace LLMClient.Abstraction;

public interface IModel
{
    string? OfficialName { get; }

    string? Publisher { get; }
}