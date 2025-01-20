using System.Windows.Media;

namespace LLMClient;

public interface ILLMEndpoint
{
    string Name { get; }

    IList<string> AvailableModels { get; }

    ILLMModel? GetModel(string modelName);
}

public interface ILLMClient
{
    bool IsResponsing { get; }
    string PreResponse { get; }

    //为了尽可能抽象，要求单个方法就传递一次会话所需要的所有参数，防止文本生成、图像生成等任务类型的不相容
    Task<string> SendRequest(IEnumerable<DialogItem> dialogItems, string prompt, string? systemPrompt = null,
        CancellationToken cancellationToken = default);
}

public interface ILLMModel
{
    string Name { get; }

    string? Id { get; }

    ImageSource? Icon { get; }

    ILLMClient GetClient();

    // string GroupName { get; }
}