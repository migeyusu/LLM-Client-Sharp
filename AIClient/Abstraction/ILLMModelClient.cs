using System.Collections.ObjectModel;
using System.Windows.Media;
using LLMClient.Endpoints;
using LLMClient.UI;

namespace LLMClient.Abstraction;

public interface ILLMModelClient
{
    /// <summary>
    /// 唯一名称
    /// </summary>
    string Name { get; }

    ILLMEndpoint Endpoint { get; }
    
    ILLMModel Info { get; }
    
    bool IsResponding { get; set; }

    IModelParams Parameters { get; set; }

    ObservableCollection<string> PreResponse { get; }

    //为了尽可能抽象，要求单个方法就传递一次会话所需要的所有参数，防止文本生成、图像生成等任务类型的不相容
    Task<CompletedResult> SendRequest(IEnumerable<IDialogViewItem> dialogItems,
        CancellationToken cancellationToken = default);
}