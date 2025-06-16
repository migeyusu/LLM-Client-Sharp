using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
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

    Task<CompletedResult> SendRequest(IEnumerable<IDialogViewItem> dialogItems,
        CancellationToken cancellationToken = default);
}

public class NullLlmModelClient : ILLMModelClient
{
    public string Name { get; } = "NullLlmModelClient";

    public ILLMEndpoint Endpoint
    {
        get { return new APIEndPoint(); }
    }

    public ILLMModel Info
    {
        get { return new APIModelInfo(); }
    }

    public bool IsResponding { get; set; } = false;
    public IModelParams Parameters { get; set; } = new DefaultModelParam();
    public ObservableCollection<string> PreResponse { get; } = new ObservableCollection<string>();

    public Task<CompletedResult> SendRequest(IEnumerable<IDialogViewItem> dialogItems,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("This client does not support sending requests.");
    }
}