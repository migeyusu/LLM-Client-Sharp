using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Media;
using LLMClient.UI;

namespace LLMClient;

public interface ILLMEndpoint
{
    string Name { get; }

    IList<string> AvailableModels { get; }

    ILLMModel? GetModel(string modelName);

    Task Initialize();

    void UpdateConfig(JsonNode document);
    
    void ReloadConfig(JsonNode document);
}

public interface ILLMModel
{
    string Name { get; }

    string? Id { get; }

    ImageSource? Icon { get; }
    
    bool IsResponsing { get; }
    
    object Info { get; }
    
    ObservableCollection<string> PreResponse { get; }

    //为了尽可能抽象，要求单个方法就传递一次会话所需要的所有参数，防止文本生成、图像生成等任务类型的不相容
    Task<string> SendRequest(IEnumerable<DialogViewItem> dialogItems, string prompt, 
        CancellationToken cancellationToken = default);
}