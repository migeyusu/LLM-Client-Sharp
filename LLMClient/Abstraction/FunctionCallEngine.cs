using System.Diagnostics;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using FunctionCallContent = Microsoft.Extensions.AI.FunctionCallContent;
using FunctionResultContent = Microsoft.Extensions.AI.FunctionResultContent;
using TextContent = Microsoft.Extensions.AI.TextContent;

namespace LLMClient.Abstraction;

public class FunctionCallException : Exception
{
    public FunctionCallException(string message) : base(message)
    {
    }
}

public abstract class FunctionCallEngine
{
    public abstract bool IsToolCallMode { get; }

    /// <summary>
    /// 默认情况下，应该让LLM了解函数调用失败的情况，并继续生成内容。
    /// </summary>
    public bool IsQuitWhenFunctionCallFailed { get; set; } = false;

    public KernelPluginCollection KernelPluginCollection { get; } = [];

    public bool HasFunctions => KernelPluginCollection.Count > 0;

    public abstract void PreviewRequest(ChatOptions options, IEndpointModel model,
        IList<ChatMessage> chatMessages);

    public abstract Task<List<FunctionCallContent>> TryParseFunctionCalls(ChatResponse response);

    public abstract Task AfterProcess(ChatMessage replyMessage, IList<FunctionResultContent> results);

    public static FunctionCallEngine Create(FunctionCallEngineType engineType)
    {
        return engineType switch
        {
            FunctionCallEngineType.Prompt => new PromptFunctionCallEngine(),
            _ => new DefaultFunctionCallEngine()
        };
    }

    public static List<FunctionCallContent> ExtractFunctionCallsFromResponse(ChatResponse response)
    {
        var functionCalls = new List<FunctionCallContent>();
        foreach (var responseMessage in response.Messages)
        {
            foreach (var content in responseMessage.Contents)
            {
                if (content is FunctionCallContent functionCallContent)
                {
                    functionCalls.Add(functionCallContent);
                }
            }
        }

        return functionCalls;
    }

    public static void EncapsulateReply(ChatMessage replyMessage, IList<FunctionResultContent> results)
    {
        replyMessage.Role = ChatRole.Tool;
        for (var index = 0; index < results.Count; index++)
        {
            var functionResultContent = results[index];
            replyMessage.Contents.Insert(index, functionResultContent);
        }
    }

    public async Task ProcessFunctionCallsAsync(ChatContext chatContext, ChatMessage replyMessage,
        IList<FunctionCallContent> functionCalls, ReactStep? step = null,
        CancellationToken token = default)
    {
        #region function call

        var functionResultContents = new List<FunctionResultContent>();
        foreach (var functionCallContent in functionCalls)
        {
            if (!this.KernelPluginCollection.TryGetFunction(null, functionCallContent.Name,
                    out var kernelFunction))
            {
                var exception = new Exception("Function not exist");
                step?.EmitDiagnostic(DiagLevel.Error,
                    $"Function '{functionCallContent.Name}' not exist, call failed.");
                functionResultContents.Add(
                    new FunctionResultContent(functionCallContent.CallId, exception.HierarchicalMessage())
                        { Exception = exception });
                if (IsQuitWhenFunctionCallFailed)
                {
                    throw new FunctionCallException($"Function '{functionCallContent.Name}' not exist.");
                }
            }
            else
            {
                var additionalFunctionCallResults = chatContext.AdditionalFunctionCallResult;
                // additionalFunctionCallResults不能包含 FunctionResultContent
                Trace.Assert(additionalFunctionCallResults.All(c => c is not FunctionResultContent));
                var additionalUserMessageBuilder = chatContext.AdditionalUserMessage;
                var chatMessageContents = replyMessage.Contents;
                try
                {
                    var arguments = new AIFunctionArguments(functionCallContent.Arguments);
                    additionalFunctionCallResults.Clear();
                    additionalUserMessageBuilder.Clear();
                    var invokeResult = await kernelFunction.InvokeAsync(arguments, token);
                    step?.Emit(new FunctionCallCompleted(functionCallContent.CallId,
                        functionCallContent.Name, invokeResult, null));
                    functionResultContents.Add(new FunctionResultContent(functionCallContent.CallId,
                        invokeResult));

                    //用于特殊需求，某些函数调用后，可能需要额外的内容返回给LLM
                    var additionalUserMessage = additionalUserMessageBuilder.ToString();
                    if (!string.IsNullOrEmpty(additionalUserMessage))
                    {
                        chatMessageContents.Add(new TextContent(
                            $"For function {functionCallContent.Name} call (call id:{functionCallContent.CallId}) result: {additionalUserMessage}"));
                    }

                    foreach (var additionalFunctionCallResult in additionalFunctionCallResults)
                    {
                        chatMessageContents.Add(additionalFunctionCallResult);
                    }
                }
                catch (Exception e)
                {
                    step?.Emit(new FunctionCallCompleted(functionCallContent.CallId,
                        functionCallContent.Name, null, e));
                    functionResultContents.Add(
                        new FunctionResultContent(functionCallContent.CallId, e.HierarchicalMessage())
                            { Exception = e });
                    if (IsQuitWhenFunctionCallFailed)
                    {
                        throw new FunctionCallException(
                            $"Call interrupted as function '{functionCallContent.Name}' invocation failed: {e.HierarchicalMessage()}");
                    }
                }
            }
        }

        await this.AfterProcess(replyMessage, functionResultContents);

        #endregion
    }
}