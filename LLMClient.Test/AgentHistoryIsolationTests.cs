using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using Microsoft.Extensions.AI;
using FunctionCallContent = Microsoft.Extensions.AI.FunctionCallContent;
using FunctionResultContent = Microsoft.Extensions.AI.FunctionResultContent;
using TextContent = Microsoft.Extensions.AI.TextContent;

namespace LLMClient.Test;

/// <summary>
/// Tests for agent-aware ReAct history segmentation and compression isolation.
/// </summary>
public class AgentHistoryIsolationTests
{
    #region ReactHistorySegmenter - Agent Filtering

    [Fact]
    public void Segment_WithoutAgentIdFilter_IncludesAllRounds()
    {
        var history = CreateMultiAgentHistory();

        var segmentation = ReactHistorySegmenter.Segment(history);

        // Without agent filter, rounds are grouped by round number, so Agent-A and Agent-B
        // rounds with the same number merge: round 1 (A+B) + round 2 (A+B) = 2 rounds
        Assert.Equal(2, segmentation.Rounds.Count);
        Assert.Equal(2, segmentation.PreambleMessages.Count);
    }

    [Fact]
    public void Segment_WithAgentIdFilter_IncludesOnlyMatchingAgentRounds()
    {
        var history = CreateMultiAgentHistory();

        var segmentation = ReactHistorySegmenter.Segment(history, "Agent-A");

        // Should only have Agent-A's 2 rounds
        Assert.Equal(2, segmentation.Rounds.Count);
        Assert.All(segmentation.Rounds, round =>
        {
            Assert.All(round.AssistantMessages, msg =>
                Assert.Equal("Agent-A", msg.AdditionalProperties?["llmclient.react.agent"]?.ToString()));
        });

        // Non-matching messages should be in preamble
        var preambleRoundNumbers = segmentation.PreambleMessages
            .Where(m => m.AdditionalProperties?.ContainsKey("llmclient.react.round") == true)
            .Select(m => m.AdditionalProperties?["llmclient.react.round"])
            .ToList();
        Assert.NotEmpty(preambleRoundNumbers);
    }

    [Fact]
    public void Segment_WithAgentIdFilter_OtherAgentMessagesBecomePreamble()
    {
        var history = CreateMultiAgentHistory();

        var segmentation = ReactHistorySegmenter.Segment(history, "Agent-B");

        // Agent-B has 2 rounds
        Assert.Equal(2, segmentation.Rounds.Count);

        // All Agent-A messages (including tagged ones) should be in preamble
        var agentAMessagesInPreamble = segmentation.PreambleMessages
            .Where(m => m.AdditionalProperties?["llmclient.react.agent"]?.ToString() == "Agent-A")
            .ToList();
        Assert.Equal(4, agentAMessagesInPreamble.Count); // 2 assistant + 2 observation

        // Untagged preamble messages should still be there
        Assert.Contains(segmentation.PreambleMessages, m => m.Role == ChatRole.System);
        Assert.Contains(segmentation.PreambleMessages, m => m.Role == ChatRole.User && m.Text == "Start task");
    }

    [Fact]
    public void Segment_WithAgentIdFilter_NonExistentAgentReturnsEmptyRounds()
    {
        var history = CreateMultiAgentHistory();

        var segmentation = ReactHistorySegmenter.Segment(history, "NonExistent-Agent");

        Assert.Empty(segmentation.Rounds);
        // All tagged messages go to preamble: 2 system/user + 8 agent messages = 10
        Assert.Equal(10, segmentation.PreambleMessages.Count);
    }

    #endregion

    #region ReactHistorySegmenter - GetMaxRoundNumber

    [Fact]
    public void GetMaxRoundNumber_WithoutAgentId_ReturnsGlobalMax()
    {
        var history = CreateMultiAgentHistory();

        var maxRound = ReactHistorySegmenter.GetMaxRoundNumber(history);

        Assert.Equal(2, maxRound); // Both agents have max round 2
    }

    [Fact]
    public void GetMaxRoundNumber_WithAgentId_ReturnsAgentSpecificMax()
    {
        var history = CreateMultiAgentHistory();

        var maxRoundA = ReactHistorySegmenter.GetMaxRoundNumber(history, "Agent-A");
        var maxRoundB = ReactHistorySegmenter.GetMaxRoundNumber(history, "Agent-B");

        Assert.Equal(2, maxRoundA);
        Assert.Equal(2, maxRoundB);
    }

    [Fact]
    public void GetMaxRoundNumber_WithNonExistentAgent_ReturnsZero()
    {
        var history = CreateMultiAgentHistory();

        var maxRound = ReactHistorySegmenter.GetMaxRoundNumber(history, "NonExistent");

        Assert.Equal(0, maxRound);
    }

    [Fact]
    public void GetMaxRoundNumber_WithPartialAgentHistory_ReturnsCorrectMax()
    {
        var history = new List<ChatMessage>
        {
            CreateTaggedMessage(ChatRole.Assistant, 1, ReactHistoryMessageKind.Assistant, "Agent-X"),
            CreateTaggedMessage(ChatRole.Tool, 1, ReactHistoryMessageKind.Observation, "Agent-X"),
            CreateTaggedMessage(ChatRole.Assistant, 3, ReactHistoryMessageKind.Assistant, "Agent-X"),
            CreateTaggedMessage(ChatRole.Tool, 3, ReactHistoryMessageKind.Observation, "Agent-X"),
            // Missing round 2
        };

        var maxRound = ReactHistorySegmenter.GetMaxRoundNumber(history, "Agent-X");

        Assert.Equal(3, maxRound);
    }

    #endregion

    #region ReactHistorySegmenter - TagMessage with AgentId

    [Fact]
    public void TagMessage_WithAgentId_StoresAgentInMetadata()
    {
        var message = new ChatMessage(ChatRole.Assistant, "test");

        ReactHistorySegmenter.TagMessage(message, 1, ReactHistoryMessageKind.Assistant, "Test-Agent");

        Assert.Equal(1, message.AdditionalProperties?["llmclient.react.round"]);
        Assert.Equal("Assistant", message.AdditionalProperties?["llmclient.react.kind"]?.ToString());
        Assert.Equal("Test-Agent", message.AdditionalProperties?["llmclient.react.agent"]?.ToString());
    }

    [Fact]
    public void TagMessage_WithoutAgentId_DoesNotStoreAgentKey()
    {
        var message = new ChatMessage(ChatRole.Assistant, "test");

        ReactHistorySegmenter.TagMessage(message, 1, ReactHistoryMessageKind.Assistant);

        // The agent key should not be stored when agentId is null
        Assert.False(message.AdditionalProperties?.ContainsKey("llmclient.react.agent") ?? false);
    }

    [Fact]
    public void TagMessages_WithAgentId_AppliesToAllMessages()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.Assistant, "msg1"),
            new(ChatRole.Assistant, "msg2"),
        };

        ReactHistorySegmenter.TagMessages(messages, 1, ReactHistoryMessageKind.Assistant, "Batch-Agent");

        Assert.All(messages, msg =>
            Assert.Equal("Batch-Agent", msg.AdditionalProperties?["llmclient.react.agent"]?.ToString()));
    }

    #endregion

    #region Multi-Agent Round Number Isolation

    [Fact]
    public void Segment_TwoAgentsWithSameRoundNumbers_AreNotMerged()
    {
        var history = new List<ChatMessage>();

        // Agent-A Round 1
        history.Add(CreateTaggedMessage(ChatRole.Assistant, 1, ReactHistoryMessageKind.Assistant, "Agent-A", "A-thinking-1"));
        history.Add(CreateTaggedMessage(ChatRole.Tool, 1, ReactHistoryMessageKind.Observation, "Agent-A", "A-obs-1"));

        // Agent-B Round 1
        history.Add(CreateTaggedMessage(ChatRole.Assistant, 1, ReactHistoryMessageKind.Assistant, "Agent-B", "B-thinking-1"));
        history.Add(CreateTaggedMessage(ChatRole.Tool, 1, ReactHistoryMessageKind.Observation, "Agent-B", "B-obs-1"));

        // Without agent filter - both rounds with number 1 should exist as separate rounds
        // But wait - they have the same round number, so they would be merged in the old code!
        // With our changes, without filter they still merge (backward compat)
        var segmentationNoFilter = ReactHistorySegmenter.Segment(history);
        Assert.Single(segmentationNoFilter.Rounds); // Still merges without filter

        // With Agent-A filter
        var segmentationA = ReactHistorySegmenter.Segment(history, "Agent-A");
        Assert.Single(segmentationA.Rounds);
        Assert.Equal(1, segmentationA.Rounds[0].RoundNumber);
        Assert.All(segmentationA.Rounds[0].Messages, m =>
            Assert.Equal("Agent-A", m.AdditionalProperties?["llmclient.react.agent"]?.ToString()));

        // With Agent-B filter
        var segmentationB = ReactHistorySegmenter.Segment(history, "Agent-B");
        Assert.Single(segmentationB.Rounds);
        Assert.Equal(1, segmentationB.Rounds[0].RoundNumber);
        Assert.All(segmentationB.Rounds[0].Messages, m =>
            Assert.Equal("Agent-B", m.AdditionalProperties?["llmclient.react.agent"]?.ToString()));
    }

    [Fact]
    public void Segment_TwoAgentsWithInterleavedRounds_MaintainsCorrectGrouping()
    {
        var history = new List<ChatMessage>();

        // Agent-A Round 1
        var a1 = CreateTaggedMessage(ChatRole.Assistant, 1, ReactHistoryMessageKind.Assistant, "Agent-A");
        history.Add(a1);

        // Agent-B Round 1 (interleaved)
        var b1 = CreateTaggedMessage(ChatRole.Assistant, 1, ReactHistoryMessageKind.Assistant, "Agent-B");
        history.Add(b1);

        // Agent-A Round 1 observation
        var a1obs = CreateTaggedMessage(ChatRole.Tool, 1, ReactHistoryMessageKind.Observation, "Agent-A");
        history.Add(a1obs);

        // Agent-B Round 1 observation
        var b1obs = CreateTaggedMessage(ChatRole.Tool, 1, ReactHistoryMessageKind.Observation, "Agent-B");
        history.Add(b1obs);

        var segA = ReactHistorySegmenter.Segment(history, "Agent-A");
        Assert.Single(segA.Rounds);
        Assert.Equal(2, segA.Rounds[0].Messages.Count());
        Assert.Contains(a1, segA.Rounds[0].Messages);
        Assert.Contains(a1obs, segA.Rounds[0].Messages);
        Assert.DoesNotContain(b1, segA.Rounds[0].Messages);
        Assert.DoesNotContain(b1obs, segA.Rounds[0].Messages);
    }

    #endregion

    #region Backward Compatibility

    [Fact]
    public void Segment_LegacyMessagesWithoutAgentId_AreHandledCorrectly()
    {
        var history = new List<ChatMessage>();

        // Legacy messages (no agent id)
        var legacyAssistant = new ChatMessage(ChatRole.Assistant, "legacy");
        ReactHistorySegmenter.TagMessage(legacyAssistant, 1, ReactHistoryMessageKind.Assistant);
        history.Add(legacyAssistant);

        var legacyObs = new ChatMessage(ChatRole.Tool, "legacy result");
        ReactHistorySegmenter.TagMessage(legacyObs, 1, ReactHistoryMessageKind.Observation);
        history.Add(legacyObs);

        // Should still work without agent filter
        var segmentation = ReactHistorySegmenter.Segment(history);
        Assert.Single(segmentation.Rounds);

        // With agent filter, legacy messages should go to preamble (they have no agent)
        var filtered = ReactHistorySegmenter.Segment(history, "SomeAgent");
        Assert.Empty(filtered.Rounds);
        Assert.Contains(legacyAssistant, filtered.PreambleMessages);
        Assert.Contains(legacyObs, filtered.PreambleMessages);
    }

    [Fact]
    public void GetMaxRoundNumber_LegacyMessagesWithoutAgentId_ReturnsCorrectMax()
    {
        var history = new List<ChatMessage>();

        var legacyMsg = new ChatMessage(ChatRole.Assistant, "legacy");
        ReactHistorySegmenter.TagMessage(legacyMsg, 5, ReactHistoryMessageKind.Assistant);
        history.Add(legacyMsg);

        var maxNoAgent = ReactHistorySegmenter.GetMaxRoundNumber(history);
        Assert.Equal(5, maxNoAgent);

        // With agent filter, legacy messages are ignored
        var maxWithAgent = ReactHistorySegmenter.GetMaxRoundNumber(history, "AnyAgent");
        Assert.Equal(0, maxWithAgent);
    }

    #endregion

    #region RequestContext AgentId

    [Fact]
    public void RequestContext_AgentId_CanBeSetAndRead()
    {
        var context = new RequestContext
        {
            ChatMessages = new List<ChatMessage>(),
            FunctionCallEngine = new DefaultFunctionCallEngine(),
            RequestOptions = new ChatOptions(),
            AgentId = "Test-Agent",
        };

        Assert.Equal("Test-Agent", context.AgentId);
    }

    [Fact]
    public void RequestContext_AgentId_DefaultsToNull()
    {
        var context = new RequestContext
        {
            ChatMessages = new List<ChatMessage>(),
            FunctionCallEngine = new DefaultFunctionCallEngine(),
            RequestOptions = new ChatOptions(),
        };

        Assert.Null(context.AgentId);
    }

    #endregion

    #region ChatHistoryCompressionContext AgentId

    [Fact]
    public void ChatHistoryCompressionContext_AgentId_IsPassedThrough()
    {
        var context = new ChatHistoryCompressionContext
        {
            ChatHistory = new List<ChatMessage>(),
            Options = new ReactHistoryCompressionOptions(),
            CurrentRound = 1,
            CurrentClient = new SummaryOnlyLlmClient("test"),
            AgentId = "Compression-Agent",
        };

        Assert.Equal("Compression-Agent", context.AgentId);
    }

    #endregion

    #region Helper Methods

    private static List<ChatMessage> CreateMultiAgentHistory()
    {
        var history = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            new(ChatRole.User, "Start task"),
        };

        // Agent-A Round 1
        history.Add(CreateTaggedMessage(ChatRole.Assistant, 1, ReactHistoryMessageKind.Assistant, "Agent-A", "A-reasoning-1"));
        history.Add(CreateTaggedMessage(ChatRole.Tool, 1, ReactHistoryMessageKind.Observation, "Agent-A", "A-result-1"));

        // Agent-B Round 1
        history.Add(CreateTaggedMessage(ChatRole.Assistant, 1, ReactHistoryMessageKind.Assistant, "Agent-B", "B-reasoning-1"));
        history.Add(CreateTaggedMessage(ChatRole.Tool, 1, ReactHistoryMessageKind.Observation, "Agent-B", "B-result-1"));

        // Agent-A Round 2
        history.Add(CreateTaggedMessage(ChatRole.Assistant, 2, ReactHistoryMessageKind.Assistant, "Agent-A", "A-reasoning-2"));
        history.Add(CreateTaggedMessage(ChatRole.Tool, 2, ReactHistoryMessageKind.Observation, "Agent-A", "A-result-2"));

        // Agent-B Round 2
        history.Add(CreateTaggedMessage(ChatRole.Assistant, 2, ReactHistoryMessageKind.Assistant, "Agent-B", "B-reasoning-2"));
        history.Add(CreateTaggedMessage(ChatRole.Tool, 2, ReactHistoryMessageKind.Observation, "Agent-B", "B-result-2"));

        return history;
    }

    private static ChatMessage CreateTaggedMessage(ChatRole role, int roundNumber, ReactHistoryMessageKind kind, string? agentId = null, string text = "")
    {
        var message = new ChatMessage(role, text);
        ReactHistorySegmenter.TagMessage(message, roundNumber, kind, agentId);
        return message;
    }

    private sealed class SummaryOnlyLlmClient : ILLMChatClient
    {
        private readonly string _summaryText;

        public SummaryOnlyLlmClient(string summaryText)
        {
            _summaryText = summaryText;
        }

        public string Name => "SummaryOnlyLlmClient";
        public ILLMAPIEndpoint Endpoint => EmptyLLMEndpoint.Instance;
        public IEndpointModel Model { get; } = new APIModelInfo
        {
            APIId = "summary-model",
            Name = "Summary Model",
            Endpoint = EmptyLLMEndpoint.Instance,
            SupportFunctionCall = false,
            SupportStreaming = false,
            SupportSystemPrompt = true,
            SupportTextGeneration = true,
        };
        public IModelParams Parameters { get; set; } = new DefaultModelParam { Streaming = false };
        public bool IsResponding { get; set; }

        public async IAsyncEnumerable<ReactStep> SendRequestAsync(IRequestContext requestContext,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var step = new ReactStep();
            step.EmitText(_summaryText);
            step.Complete(new StepResult
            {
                FinishReason = ChatFinishReason.Stop,
                IsCompleted = true,
                Messages = [new ChatMessage(ChatRole.Assistant, _summaryText)],
            });
            yield return step;
            await Task.CompletedTask;
        }
    }

    #endregion
}
