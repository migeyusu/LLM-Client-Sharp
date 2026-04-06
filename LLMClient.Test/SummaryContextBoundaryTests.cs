using LLMClient.Agent;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
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

    private sealed class SummaryTestSession(IRequestItem requestItem) : ITextDialogSession
    {
        public IReadOnlyList<IDialogItem> DialogItems { get; } = [requestItem];

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

        public string? SystemPrompt => null;
    }

    private sealed class TestResponseItem(Guid interactionId, string content) : BaseDialogItem, IResponseItem
    {
        public override long Tokens => content.Length;

        public override ChatRole Role { get; } = ChatRole.Assistant;

        public override IEnumerable<ChatMessage> Messages
        {
            get
            {
                yield return new ChatMessage(ChatRole.Assistant, content);
            }
        }

        public override bool IsAvailableInContext { get; } = true;

        public bool IsResponding { get; } = false;

        public Guid InteractionId { get; set; } = interactionId;
    }
}

