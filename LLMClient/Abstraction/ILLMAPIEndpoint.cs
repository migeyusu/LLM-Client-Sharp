using LLMClient.Component.CustomControl;

namespace LLMClient.Abstraction;

public interface ILLMAPIEndpoint
{
    string DisplayName { get; }

    bool IsInbuilt { get; }

    bool IsEnabled { get; }

    /// <summary>
    /// 名称，必须唯一
    /// </summary>
    string Name { get; }

    ThemedIcon Icon { get; }
    
    IReadOnlyCollection<IEndpointModel> AvailableModels { get; }

    ILLMChatClient? NewChatClient(IEndpointModel model);

    IEndpointModel? GetModel(string modelName);

    Task InitializeAsync();
}