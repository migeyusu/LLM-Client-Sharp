using LLMClient.Component.CustomControl;

namespace LLMClient.Abstraction;

public interface IAPIEndpoint
{
    string DisplayName { get; }

    bool IsInbuilt { get; }

    bool IsEnabled { get; }

    /// <summary>
    /// 禁用后不会出现在可用终结点列表中
    /// </summary>
    bool IsDisabled { get; set; }

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