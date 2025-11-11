namespace LLMClient.Abstraction;

public interface IResearchModelService
{
    IReadOnlyList<string> AvailableResearchModels { get; }

    ILLMChatClient CreateResearchClient(string modelName, ILLMChatClient client);
}