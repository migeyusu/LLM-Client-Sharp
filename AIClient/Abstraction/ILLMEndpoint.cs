using System.Windows.Media;

namespace LLMClient.Abstraction;

public interface ILLMEndpoint
{
    string DisplayName { get; }

    bool IsDefault { get; }

    /// <summary>
    /// 名称，必须唯一
    /// </summary>
    string Name { get; }

    ImageSource Icon { get; }

    IReadOnlyCollection<string> AvailableModelNames { get; }

    ILLMClient? NewClient(string modelName);

    ILLMModel? GetModel(string modelName);

    Task InitializeAsync();
}