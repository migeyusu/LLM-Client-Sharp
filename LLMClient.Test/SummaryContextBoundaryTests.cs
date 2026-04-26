using LLMClient.Abstraction;
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
    public async Task SummaryAgent_CutsContextAfterSuccessfulCompletion()
    {
        var request = new RequestViewItem("summarize")
        {
            InteractionId = Guid.NewGuid(),
        };
        var session = new SummaryTestSession(request);
        var agent = new SummaryAgent(EmptyLlmModelClient.Instance);

        await foreach (var step in agent.Execute(session))
        {
            await foreach (var _ in step)
            {
            }
        }

        Assert.Same(request, session.CutContextRequest);
    }

    [Fact]
    public void GetChatHistory_KeepsSummaryInteractionButCutsOffEarlierHistoryAtEraseMarker()
    {
        var root = new RootDialogItem();
        var firstInteractionId = Guid.NewGuid();
        var summaryInteractionId = Guid.NewGuid();
        var followUpInteractionId = Guid.NewGuid();

        var firstRequest = new RequestViewItem("first")
        {
            InteractionId = firstInteractionId,
        };
        var firstResponse = new TestResponseItem(firstInteractionId, "first-response");
        var eraseMarker = new EraseViewItem();
        var summaryRequest = new RequestViewItem("summarize")
        {
            InteractionId = summaryInteractionId,
        };
        var summaryResponse = new TestResponseItem(summaryInteractionId, "summary-response");
        var followUpRequest = new RequestViewItem("follow-up")
        {
            InteractionId = followUpInteractionId,
        };
        var followUpResponse = new TestResponseItem(followUpInteractionId, "follow-up-response");

        root.AppendChild(firstRequest)
            .AppendChild(firstResponse)
            .AppendChild(eraseMarker)
            .AppendChild(summaryRequest)
            .AppendChild(summaryResponse)
            .AppendChild(followUpRequest)
            .AppendChild(followUpResponse);

        var history = followUpResponse.GetChatHistory().ToArray();

        Assert.DoesNotContain(firstRequest, history);
        Assert.DoesNotContain(firstResponse, history);
        Assert.Contains(summaryRequest, history);
        Assert.Contains(summaryResponse, history);
        Assert.Contains(followUpRequest, history);
    }

    private sealed class SummaryTestSession(IRequestItem requestItem) : IDialogSession
    {
        public Guid ID { get; } = Guid.NewGuid();
        public IReadOnlyList<IDialogItem> VisualDialogItems { get; } = [requestItem];
        public IResponseItem WorkingResponse => throw new NotSupportedException();

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

    private sealed class TestResponseItem(Guid interactionId, string content) : BaseDialogItem, IResponseItem
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