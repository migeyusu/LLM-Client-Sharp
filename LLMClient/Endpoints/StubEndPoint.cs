using System.Windows.Media;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;

namespace LLMClient.Endpoints;

public class StubEndPoint : ILLMAPIEndpoint
{
    public StubEndPoint(IReadOnlyCollection<ILLMModel> availableModels)
    {
        AvailableModels = availableModels;
    }

    public required string DisplayName { get; set; }
    public bool IsInbuilt { get; } = true;
    public bool IsEnabled { get; } = true;
    public required string Name { get; set; }
    public required ThemedIcon Icon { get; set; }

    public IReadOnlyCollection<ILLMModel> AvailableModels { get; }

    public ILLMChatClient? NewChatClient(ILLMModel model)
    {
        return model.CreateChatClient();
    }

    public ILLMModel? GetModel(string modelName)
    {
        return AvailableModels.FirstOrDefault(model => model.Name == modelName);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
}