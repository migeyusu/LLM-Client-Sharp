using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Media;
using LLMClient.Endpoints;
using LLMClient.UI;
using Microsoft.Extensions.AI;

namespace LLMClient;

public interface ILLMEndpoint
{
    string DisplayName { get; }

    string Name { get; }

    ImageSource? Icon { get; }

    IList<string> AvailableModelNames { get; }

    ILLMModelClient? NewClient(string modelName);

    Task InitializeAsync();
}

public class CompletedResult
{
    public CompletedResult(string? response, UsageDetails usage)
    {
        Response = response;
        Usage = usage;
    }

    public string? Response { get; set; }

    public UsageDetails Usage { get; set; }

    public string? ErrorMessage { get; set; }

    public bool IsInterrupt
    {
        get { return ErrorMessage != null; }
    }
}

public interface ILLMModelClient : IDialogViewItem
{
    /// <summary>
    /// 唯一名称
    /// </summary>
    string Name { get; }

    ILLMEndpoint Endpoint { get; }

    ImageSource? Icon { get; }

    bool IsResponding { get; }

    ILLMModel Info { get; }

    IModelParams Parameters { get; set; }

    ObservableCollection<string> PreResponse { get; }

    //为了尽可能抽象，要求单个方法就传递一次会话所需要的所有参数，防止文本生成、图像生成等任务类型的不相容
    Task<CompletedResult> SendRequest(IEnumerable<IDialogViewItem> dialogItems,
        CancellationToken cancellationToken = default);
}