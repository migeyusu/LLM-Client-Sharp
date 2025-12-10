namespace LLMClient.Abstraction;

public interface IResearchModelService
{
    IReadOnlyList<string> AvailableResearchModels { get; }
}