using System.Windows.Media;

namespace LLMClient.Abstraction;

public interface ILLMEndpoint
{
    string DisplayName { get; }

    bool IsInbuilt { get; }

    bool IsEnabled { get; }

    /// <summary>
    /// 名称，必须唯一
    /// </summary>
    string Name { get; }

    ImageSource Icon { get; }
    
    IReadOnlyCollection<ILLMChatModel> AvailableModels { get; }

    ILLMChatClient? NewChatClient(ILLMChatModel model);

    ILLMChatModel? GetModel(string modelName);

    Task InitializeAsync();
}