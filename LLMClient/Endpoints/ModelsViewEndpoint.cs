using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;

namespace LLMClient.Endpoints;

/// <summary>
/// 用于展示可用模型的集合，通常作为一个特殊的endpoint存在，允许用户选择不同的模型进行对话。
/// </summary>
public class ModelsViewEndpoint : ILLMAPIEndpoint
{
    public ModelsViewEndpoint(IReadOnlyCollection<IEndpointModel> availableModels)
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