using System.Windows.Media;

namespace LLMClient.Abstraction;

public interface ILLMEndpoint
{
    string DisplayName { get; }

    bool IsDefault { get; }
    
    bool IsEnabled { get; }

    /// <summary>
    /// 名称，必须唯一
    /// </summary>
    string Name { get; }

    ImageSource Icon { get; }

    IReadOnlyCollection<string> AvailableModelNames { get; }

    IReadOnlyCollection<ILLMModel> AvailableModels { get; }

    ILLMClient? NewClient(string modelName);

    ILLMClient? NewClient(ILLMModel model);

    ILLMModel? GetModel(string modelName);

    Task InitializeAsync();
}