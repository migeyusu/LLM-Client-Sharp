using System.Windows.Media;
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
    
    IReadOnlyCollection<ILLMModel> AvailableModels { get; }

    ILLMChatClient? NewChatClient(ILLMModel model);

    ILLMModel? GetModel(string modelName);

    Task InitializeAsync();
}