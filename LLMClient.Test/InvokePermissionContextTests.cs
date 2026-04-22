using LLMClient.Abstraction;
using LLMClient.Configuration;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;

namespace LLMClient.Test;

public class InvokePermissionContextTests
{
    [Fact]
    public void CreateForRequest_MergesRequestAndParentAutoApprove()
    {
        var requestContext = new RequestContext
        {
            ChatMessages = [],
            FunctionCallEngine = FunctionCallEngine.Create(FunctionCallEngineType.Prompt),
            RequestOptions = new ChatOptions(),
            AutoApproveAllInvocations = false,
            ShowRequestJson = true,
        };
        var parentContext = new ChatContext
        {
            AutoApproveAllInvocations = true,
        };

        var chatContext = ChatContext.CreateForRequest(requestContext, null, true, parentContext);

        Assert.True(chatContext.AutoApproveAllInvocations);
        Assert.True(chatContext.ShowRequestJson);
        Assert.True(chatContext.Streaming);
    }
}

