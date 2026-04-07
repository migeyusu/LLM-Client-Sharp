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
    public void MapFromRequest_CopiesAutoApprove_FromRequestViewItem()
    {
        var request = new RequestViewItem("test")
        {
            AutoApproveAllInvocations = true,
            IsDebugMode = true,
        };
        var builder = new DefaultDialogContextBuilder([request]);

        builder.MapFromRequest(request);

        Assert.True(builder.AutoApproveAllInvocations);
        Assert.True(builder.IsDebugMode);
    }

    [Fact]
    public void CreateForRequest_MergesRequestAndParentAutoApprove()
    {
        var requestContext = new RequestContext
        {
            ChatHistory = [],
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

