using LLMClient.Abstraction;
using Microsoft.Extensions.AI;
using FunctionCallContent = Microsoft.Extensions.AI.FunctionCallContent;
using FunctionResultContent = Microsoft.Extensions.AI.FunctionResultContent;

namespace LLMClient.Endpoints;

public class DefaultFunctionCallEngine : FunctionCallEngine
{
    public DefaultFunctionCallEngine()
    {
    }

    public override bool IsToolCallMode { get; } = true;

    public override void PreviewRequest(ChatOptions options, IEndpointModel model, IList<ChatMessage> chatMessages)
    {
        options.Tools = KernelPluginCollection.SelectMany(plugin => plugin).ToArray<AITool>();
    }

    public override Task<List<FunctionCallContent>> TryParseFunctionCalls(ChatResponse response)
    {
        return Task.FromResult(ExtractFunctionCallsFromResponse(response));
    }

    public override Task AfterProcess(ChatMessage replyMessage, IList<FunctionResultContent> results)
    {
        EncapsulateReply(replyMessage, results);
        return Task.CompletedTask;
    }
}