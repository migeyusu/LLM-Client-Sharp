using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Media;
using LLMClient.UI;

namespace LLMClient;

public interface ILLMEndpoint
{
    string DisplayName { get; }

    string Name { get; }

    IList<string> AvailableModelNames { get; }

    ILLMModelClient? NewClient(string modelName);

    Task InitializeAsync();
}

public interface ILLMModelClient : IDialogViewItem
{
    /// <summary>
    /// 唯一名称
    /// </summary>
    string Name { get; }

    ImageSource? Icon { get; }

    bool IsResponsing { get; }

    object? Info { get; }

    int TotalTokens { get; }

    int PromptTokens { get; }

    int CompletionTokens { get; }

    ObservableCollection<string> PreResponse { get; }

    void Deserialize(IModelParams info);

    IModelParams Serialize();

    //为了尽可能抽象，要求单个方法就传递一次会话所需要的所有参数，防止文本生成、图像生成等任务类型的不相容
    Task<string> SendRequest(IEnumerable<IDialogViewItem> dialogItems,
        CancellationToken cancellationToken = default);
}