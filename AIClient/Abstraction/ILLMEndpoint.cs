using System.Windows.Media;

namespace LLMClient.Abstraction;

public interface ILLMEndpoint
{
    string DisplayName { get; }

    /// <summary>
    /// 名称，必须唯一
    /// </summary>
    string Name { get; }

    ImageSource Icon { get; }

    IList<string> AvailableModelNames { get; }

    ILLMModelClient? NewClient(string modelName);

    ILLMModel? GetModel(string modelName);

    Task InitializeAsync();
}