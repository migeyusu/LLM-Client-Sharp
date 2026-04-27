﻿﻿﻿using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.ToolCall;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace LLMClient.Test;

public class SummaryContextBoundaryTests
{
    [Fact]
    public async Task SummaryAgent_DoesNotCallCutContext()
    {
        var interactionId = Guid.NewGuid();
        var root = new RootDialogItem();
        var request = new RequestViewItem("summarize")
        {
            InteractionId = interactionId,
        };
        var workingResponse = new TestResponseItem(interactionId, "test");
        root.AppendChild(request).AppendChild(workingResponse);
        var session = new SummaryTestSession(request, workingResponse);
        var agent = new SummaryAgent(EmptyLlmModelClient.Instance);

        try
        {
            await foreach (var step in agent.Execute(session, AgentRunOption.Default))
            {
                await foreach (var _ in step)
                {
                }
            }
        }
        catch (NotSupportedException)
        {
            // EmptyLlmModelClient 抛出 NotSupportedException，这是预期的
        }

        // Agent 不应再调用 CutContextAsync
        Assert.Null(session.CutContextRequest);
    }

    [Fact]
    public void SummaryRequestViewItem_DuringSummarizing_ActAsNormalRequest()
    {
        var root = new RootDialogItem();
        var interactionId = Guid.NewGuid();
        var summaryInteractionId = Guid.NewGuid();

        var firstRequest = new RequestViewItem("first")
        {
            InteractionId = interactionId,
        };
        var firstResponse = new TestResponseItem(interactionId, "first-response");
        var summaryRequest = new SummaryRequestViewItem(new RequestViewItem("summarize")
        {
            InteractionId = summaryInteractionId,
        })
        {
            InteractionId = summaryInteractionId,
            State = SummaryRequestState.Summarizing,
        };
        var summaryResponse = new TestResponseItem(summaryInteractionId, "summary-response");

        root.AppendChild(firstRequest)
            .AppendChild(firstResponse)
            .AppendChild(summaryRequest)
            .AppendChild(summaryResponse);

        // 正在总结时，SummaryRequestViewItem 被视为普通请求
        var history = summaryResponse.GetDialogHistory().ToArray();

        Assert.Contains(firstRequest, history);
        Assert.Contains(firstResponse, history);
        Assert.Contains(summaryRequest, history);
    }

    [Fact]
    public void SummaryRequestViewItem_AfterCompleted_ActAsTruncationBoundary()
    {
        var root = new RootDialogItem();
        var interactionId = Guid.NewGuid();
        var summaryInteractionId = Guid.NewGuid();
        var followUpInteractionId = Guid.NewGuid();

        var firstRequest = new RequestViewItem("first")
        {
            InteractionId = interactionId,
        };
        var firstResponse = new TestResponseItem(interactionId, "first-response");
        var summaryRequest = new SummaryRequestViewItem(new RequestViewItem("summarize")
        {
            InteractionId = summaryInteractionId,
        })
        {
            InteractionId = summaryInteractionId,
            State = SummaryRequestState.Completed,
        };
        var summaryResponse = new TestResponseItem(summaryInteractionId, "summary-response");
        var followUpRequest = new RequestViewItem("follow-up")
        {
            InteractionId = followUpInteractionId,
        };
        var followUpResponse = new TestResponseItem(followUpInteractionId, "follow-up-response");

        root.AppendChild(firstRequest)
            .AppendChild(firstResponse)
            .AppendChild(summaryRequest)
            .AppendChild(summaryResponse)
            .AppendChild(followUpRequest)
            .AppendChild(followUpResponse);

        var history = followUpResponse.GetDialogHistory().ToArray();

        // 总结完成后，SummaryRequestViewItem 充当截断边界
        Assert.DoesNotContain(firstRequest, history);
        Assert.DoesNotContain(firstResponse, history);
        // SummaryRequestViewItem 本身作为边界不被包含
        Assert.DoesNotContain(summaryRequest, history);
        // 但总结回复（包含总结内容）仍然在历史中
        Assert.Contains(summaryResponse, history);
        Assert.Contains(followUpRequest, history);
    }

    [Fact]
    public void SummaryRequestViewItem_OnFailed_SkipsAndContinues()
    {
        var root = new RootDialogItem();
        var interactionId = Guid.NewGuid();
        var failedInteractionId = Guid.NewGuid();
        var followUpInteractionId = Guid.NewGuid();

        var firstRequest = new RequestViewItem("first")
        {
            InteractionId = interactionId,
        };
        var firstResponse = new TestResponseItem(interactionId, "first-response");
        var failedRequest = new SummaryRequestViewItem(new RequestViewItem("summarize")
        {
            InteractionId = failedInteractionId,
        })
        {
            InteractionId = failedInteractionId,
            State = SummaryRequestState.Failed,
        };
        var failedResponse = new TestResponseItem(failedInteractionId, "failed-response");
        var followUpRequest = new RequestViewItem("follow-up")
        {
            InteractionId = followUpInteractionId,
        };
        var followUpResponse = new TestResponseItem(followUpInteractionId, "follow-up-response");

        root.AppendChild(firstRequest)
            .AppendChild(firstResponse)
            .AppendChild(failedRequest)
            .AppendChild(failedResponse)
            .AppendChild(followUpRequest)
            .AppendChild(followUpResponse);

        var history = followUpResponse.GetDialogHistory().ToArray();

        // 失败时跳过 SummaryRequestViewItem，继续向前遍历
        Assert.Contains(firstRequest, history);
        Assert.Contains(firstResponse, history);
        Assert.DoesNotContain(failedRequest, history);
        Assert.Contains(failedResponse, history);
        Assert.Contains(followUpRequest, history);
    }

    private sealed class SummaryTestSession(IRequestItem requestItem, TestResponseItem? workingResponse = null) : IDialogSession
    {
        private readonly TestResponseItem _workingResponse = workingResponse ?? new TestResponseItem(requestItem.InteractionId, "test");
        
        public Guid ID { get; } = Guid.NewGuid();
        public IReadOnlyList<IDialogItem> VisualDialogItems { get; } = [requestItem];
        public IResponseItem WorkingResponse => _workingResponse;

        public string? WorkingDirectory { get; } = null;

        public IRequestItem? CutContextRequest { get; private set; }

        public List<IChatHistoryItem> GetHistory()
        {
            return [requestItem];
        }

        public Task CutContextAsync(IRequestItem? targetRequestItem = null)
        {
            CutContextRequest = targetRequestItem;
            return Task.CompletedTask;
        }

        public AIContextProvider[]? ContextProviders { get; } = null;
        
        public IPromptCommandAggregate? PromptCommand { get; set; }

        public string? SystemPrompt => null;

        public IEnumerable<Type> SupportedAgents { get; } = Array.Empty<Type>();

        public IFunctionGroupSource? ToolsSource { get; } = null;

        public Task<IResponse> NewResponse(RequestOption option, IRequestItem? insertBefore = null,
            CancellationToken token = default)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class TestResponseItem(Guid interactionId, string content, IDialogItem? parent = null) : BaseDialogItem, IResponseItem
    {
        public override long Tokens => content.Length;

        public override DialogRole Role { get; } = DialogRole.Response;

        public override IEnumerable<ChatMessage> Messages
        {
            get { yield return new ChatMessage(ChatRole.Assistant, content); }
        }

        public override IDialogSession? Session { get; } = null;
        public override bool IsAvailableInContext { get; } = true;

        public bool IsResponding { get; } = false;

        public Guid InteractionId { get; set; } = interactionId;
    }
}
