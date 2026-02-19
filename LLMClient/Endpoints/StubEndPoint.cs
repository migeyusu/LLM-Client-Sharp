using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using MaterialDesignThemes.Wpf;

namespace LLMClient.Endpoints;

public class StubEndPoint: ILLMAPIEndpoint
{
    public static StubEndPoint Instance { get; } = new("Stub Endpoint");

    public StubEndPoint(string name)
    {
        Name = name;
        Icon = LocalThemedIcon.FromPackIcon(PackIconKind.TestTube);
    }

    public string DisplayName
    {
        get { return Name; }
    }

    public bool IsInbuilt { get; } = true;
    public bool IsEnabled { get; } = true;
    public string Name { get; }
    public ThemedIcon Icon { get; }

    public IReadOnlyCollection<IEndpointModel> AvailableModels => [StubLLMChatModel.Instance];

    public ILLMChatClient? NewChatClient(IEndpointModel model)
    {
        return new StubLlmClient();
    }

    public IEndpointModel? GetModel(string modelName)
    {
        return new StubLLMChatModel();
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
}