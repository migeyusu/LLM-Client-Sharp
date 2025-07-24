using System.Collections.ObjectModel;
using LLMClient.Abstraction;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI.Dialog;
using LLMClient.UI.MCP;
using Microsoft.Extensions.AI;

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

    public IFunctionInterceptor FunctionInterceptor { get; set; } = FunctionAuthorizationInterceptor.Instance;
    public ObservableCollection<string> RespondingText { get; } = new ObservableCollection<string>();

    public Task<CompletedResult> SendRequest(DialogContext context,
        CancellationToken cancellationToken = default)
    {
        // 模拟一个完成的结果
        var result = new CompletedResult(new UsageDetails(), new List<ChatMessage>
        {
            new ChatMessage(ChatRole.Assistant, "This is a test response from the LLM client.")
        })
        {
            FinishReason = ChatFinishReason.Stop,
            Latency = 100,
            Duration = 200,
            Price = 0.01
        };

        return Task.FromResult(result);
    }
}