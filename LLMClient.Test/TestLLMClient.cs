using System.Collections.ObjectModel;
using LLMClient.Abstraction;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI.Dialog;

namespace LLMClient.Test;

public class TestLLMClient : ILLMClient
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; } = "Test LLM Client";

    public ILLMEndpoint Endpoint
    {
        get { return new NullLLMEndpoint(); }
    }

    public ILLMModel Model
    {
        get { return new APIModelInfo(); }
    }

    public bool IsResponding { get; set; } = false;
    public IModelParams Parameters { get; set; } = new DefaultModelParam();
    public ObservableCollection<string> RespondingText { get; } = new ObservableCollection<string>();

    public Task<CompletedResult> SendRequest(IList<IDialogItem> dialogItems, string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}