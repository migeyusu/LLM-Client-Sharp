using System.Windows.Media;
using LLMClient.Abstraction;

namespace LLMClient.Endpoints;

public class StubEndPoint : ILLMAPIEndpoint
{
    public StubEndPoint(IReadOnlyCollection<ILLMChatModel> availableModels)
    {
        AvailableModels = availableModels;
    }

    public required string DisplayName { get; set; }
    public bool IsInbuilt { get; } = true;
    public bool IsEnabled { get; } = true;
    public required string Name { get; set; }
    public required ImageSource Icon { get; set; }

    public IReadOnlyCollection<ILLMChatModel> AvailableModels { get; }

    public ILLMChatClient? NewChatClient(ILLMChatModel model)
    {
        return model.CreateChatClient();
    }

    public ILLMChatModel? GetModel(string modelName)
    {
        return AvailableModels.FirstOrDefault(model => model.Name == modelName);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
}