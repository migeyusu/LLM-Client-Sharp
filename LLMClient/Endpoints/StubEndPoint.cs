using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;

namespace LLMClient.Endpoints;

public class StubEndPoint : ILLMAPIEndpoint
{
    public StubEndPoint(IReadOnlyCollection<IEndpointModel> availableModels)
    {
        AvailableModels = availableModels;
    }

    public required string DisplayName { get; set; }
    public bool IsInbuilt { get; } = true;
    public bool IsEnabled { get; } = true;
    public required string Name { get; set; }
    public required ThemedIcon Icon { get; set; }

    public IReadOnlyCollection<IEndpointModel> AvailableModels { get; }

    public ILLMChatClient? NewChatClient(IEndpointModel model)
    {
        return model.CreateChatClient();
    }

    public IEndpointModel? GetModel(string modelName)
    {
        return AvailableModels.FirstOrDefault(model => model.Name == modelName);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
}