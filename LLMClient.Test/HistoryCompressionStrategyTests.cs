using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Agent.MiniSWE;
using LLMClient.Component;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Dialog.Proc;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.ToolCall;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using AIContextProvider = Microsoft.Agents.AI.AIContextProvider;
using FunctionCallContent = Microsoft.Extensions.AI.FunctionCallContent;
using FunctionResultContent = Microsoft.Extensions.AI.FunctionResultContent;
using TextContent = Microsoft.Extensions.AI.TextContent;

namespace LLMClient.Test;

public class HistoryCompressionStrategyTests
{
    public HistoryCompressionStrategyTests()
    {
        var services = new ServiceCollection()
            .AddSingleton<ITokensCounter, DefaultTokensCounter>()
            .AddSingleton<IViewModelFactory, ViewModelFactory>()
            .AddSingleton<GlobalOptions>()
            .AddSingleton<Summarizer>()
            .AddSingleton<ChatHistoryCompressionStrategyFactory>()
            .BuildServiceProvider();
        BaseViewModel.ServiceLocator = services;
    }

    [Fact]
    public async Task ObservationMasking_ReplacesOnlyOlderObservationMessages()
    {
        var chatHistory = CreateHistoryWithTwoRounds();
        var strategy = new ObservationMaskingChatHistoryCompressionStrategy();

        await strategy.CompressAsync(new ChatHistoryCompressionContext
        {
            ChatHistory = chatHistory,
            Options = new ReactHistoryCompressionOptions
            {
                Mode = ReactHistoryCompressionMode.ObservationMasking,
                PreserveRecentRounds = 1,
                ObservationPlaceholder = "[details omitted for brevity]",
            },
            CurrentRound = 2,
            CurrentClient = new SummaryOnlyLlmClient("unused"),
        });

        Assert.Contains(chatHistory.SelectMany(message => message.Contents).OfType<FunctionResultContent>(),
            content => Equals(content.Result, "[details omitted for brevity]") && content.CallId == "call-1");
        Assert.Contains(chatHistory.SelectMany(message => message.Contents).OfType<FunctionResultContent>(),
            content => Equals(content.Result, "second observation") && content.CallId == "call-2");
        Assert.DoesNotContain(chatHistory.SelectMany(message => message.Contents).OfType<FunctionResultContent>(),
            content => Equals(content.Result, "first observation"));
    }

    [Fact]
    public async Task InfoCleaning_ReplacesOlderRoundWithAssistantSummary()
    {
        var chatHistory = CreateHistoryWithTwoRounds();
        var summaryClient = new SummaryOnlyLlmClient("checked repository state and captured first observation");
        var summarizer = new Summarizer(new GlobalOptions());
        var strategy = new InfoCleaningChatHistoryCompressionStrategy(summarizer);

        await strategy.CompressAsync(new ChatHistoryCompressionContext
        {
            ChatHistory = chatHistory,
            Options = new ReactHistoryCompressionOptions
            {
                Mode = ReactHistoryCompressionMode.InfoCleaning,
                PreserveRecentRounds = 1,
            },
            CurrentRound = 2,
            CurrentClient = summaryClient,
        });

        var roundSummary = Assert.Single(chatHistory.Where(message =>
            message.Role == ChatRole.Assistant &&
            message.Text.StartsWith("[Round 1 summary]", StringComparison.Ordinal)));
        Assert.Equal("[Round 1 summary] checked repository state and captured first observation", roundSummary.Text);
        Assert.DoesNotContain(chatHistory.SelectMany(message => message.Contents).OfType<FunctionResultContent>(),
            content => content.CallId == "call-1");
        Assert.Contains(chatHistory.SelectMany(message => message.Contents).OfType<FunctionResultContent>(),
            content => content.CallId == "call-2");
    }

    [Fact]
    public async Task TaskSummary_UsesSummarizerToCollapseEarlierRounds()
    {
        var chatHistory = CreateHistoryWithTwoRounds();
        var summaryClient = new SummaryOnlyLlmClient("condensed task summary");
        var summarizer = new Summarizer(new GlobalOptions());
        var strategy = new TaskSummaryChatHistoryCompressionStrategy(summarizer);

        await strategy.CompressAsync(new ChatHistoryCompressionContext
        {
            ChatHistory = chatHistory,
            Options = new ReactHistoryCompressionOptions
            {
                Mode = ReactHistoryCompressionMode.TaskSummary,
                PreserveRecentRounds = 1,
            },
            CurrentRound = 2,
            CurrentClient = summaryClient,
        });

        Assert.Contains(chatHistory, message => message.Role == ChatRole.Assistant &&
                                                message.Text == "[Compressed history summary]\ncondensed task summary");
        Assert.DoesNotContain(chatHistory.SelectMany(message => message.Contents).OfType<FunctionResultContent>(),
            content => content.CallId == "call-1");
        Assert.Contains(chatHistory.SelectMany(message => message.Contents).OfType<FunctionResultContent>(),
            content => content.CallId == "call-2");
    }

    [Fact]
    public async Task LlmClientBase_ObservationMasking_CompressesHistoryBeforeNextLoop()
    {
        var chatClient = new RecordingSequentialChatClient(
            CreateToolCallResponse("call-1", "first observation"),
            CreateToolCallResponse("call-2", "second observation"),
            CreateTextResponse("done"));
        var client = new CompressionAwareLlmClient(chatClient, ReactHistoryCompressionMode.ObservationMasking);
        client.ModelInfo.HistoryCompression.PreserveRecentRounds = 1;
        client.ModelInfo.HistoryCompression.ObservationPlaceholder = "[details omitted for brevity]";
        client.ModelInfo.HistoryCompression.ReactTokenThresholdPercent = 0.0001; // very low to always trigger
        var requestContext = new RequestContext
        {
            ChatMessages = [new ChatMessage(ChatRole.User, "solve the issue")],
            FunctionCallEngine = new LoopingToolCallEngine(),
            RequestOptions = new ChatOptions(),
        };

#pragma warning disable SKEXP0001
        var result = await client.SendRequestCompatAsync(requestContext, CancellationToken.None);
#pragma warning restore SKEXP0001

        Assert.Null(result.Exception);
        Assert.Equal(3, chatClient.SeenHistories.Count);
        Assert.DoesNotContain("[details omitted for brevity]", chatClient.SeenHistories[1]);
        Assert.Contains("[details omitted for brevity]", chatClient.SeenHistories[2]);
        Assert.DoesNotContain("first observation", chatClient.SeenHistories[2]);
        Assert.Contains("second observation", chatClient.SeenHistories[2]);
    }

    [Fact]
    public async Task LlmClientBase_ToolCallRounds_DeduplicatesRepeatedThinkingInHistory()
    {
        const string repeatedThinking = "Need to inspect configuration before next action.";
        var chatClient = new RecordingSequentialChatClient(
            CreateToolCallResponse("call-1", "first observation", repeatedThinking),
            CreateToolCallResponse("call-2", "second observation", repeatedThinking),
            CreateTextResponse("done"));
        var client = new CompressionAwareLlmClient(chatClient, ReactHistoryCompressionMode.None);
        var requestContext = new RequestContext
        {
            ChatMessages = [new ChatMessage(ChatRole.User, "solve the issue")],
            FunctionCallEngine = new LoopingToolCallEngine(),
            RequestOptions = new ChatOptions(),
        };

#pragma warning disable SKEXP0001
        var result = await client.SendRequestCompatAsync(requestContext, CancellationToken.None);
#pragma warning restore SKEXP0001

        Assert.Null(result.Exception);
        Assert.Equal(3, chatClient.SeenHistories.Count);
        Assert.Equal(1, CountOccurrences(chatClient.SeenHistories[2], $"assistant:reasoning:{repeatedThinking}"));
    }

    [Fact]
    public async Task PreambleSummary_SkipsWhenBelowThreshold()
    {
        var chatHistory = CreateHistoryWithPreambleAndRounds(shortPreamble: true);
        var summaryClient = new SummaryOnlyLlmClient("should not be called");
        var summarizer = new Summarizer(new GlobalOptions());
        var strategy = new PreambleSummaryChatHistoryCompressionStrategy(summarizer, new DefaultTokensCounter());

        var originalCount = chatHistory.Count;
        await strategy.CompressAsync(new ChatHistoryCompressionContext
        {
            ChatHistory = chatHistory,
            Options = new ReactHistoryCompressionOptions
            {
                PreambleCompression = true,
                PreambleTokenThresholdPercent = 100, // very high threshold
            },
            CurrentRound = 0,
            CurrentClient = summaryClient,
        });

        // Nothing should have changed
        Assert.Equal(originalCount, chatHistory.Count);
    }

    [Fact]
    public async Task PreambleSummary_CompressesPreviousTaskContext()
    {
        var chatHistory = CreateHistoryWithPreambleAndRounds(shortPreamble: false);
        var summaryClient =
            new SummaryOnlyLlmClient("investigated auth module, fixed token refresh bug in AuthService.cs");
        var summarizer = new Summarizer(new GlobalOptions());
        var strategy = new PreambleSummaryChatHistoryCompressionStrategy(summarizer, new DefaultTokensCounter());

        await strategy.CompressAsync(new ChatHistoryCompressionContext
        {
            ChatHistory = chatHistory,
            Options = new ReactHistoryCompressionOptions
            {
                PreambleCompression = true,
                PreambleTokenThresholdPercent = 0.0001, // very low threshold to trigger compression
            },
            CurrentRound = 0,
            CurrentClient = summaryClient,
        });

        // System prompt should be preserved
        Assert.Equal(ChatRole.System, chatHistory[0].Role);
        Assert.Equal("You are helpful.", chatHistory[0].Text);

        // Previous task context should be replaced with summary
        Assert.Contains(chatHistory, message =>
            message.Role == ChatRole.Assistant &&
            message.Text.Contains("[Previous task context summary]") &&
            message.Text.Contains("investigated auth module"));

        // Current task user message should be preserved
        Assert.Contains(chatHistory, message =>
            message.Role == ChatRole.User &&
            message.Text == "Now fix the CI pipeline.");

        // Current task rounds should be preserved
        Assert.Contains(chatHistory.SelectMany(message => message.Contents).OfType<FunctionCallContent>(),
            content => content.CallId == "call-current-1");

        // Previous task messages should be removed
        Assert.DoesNotContain(chatHistory, message =>
            message.Text == "Fix the authentication bug.");
        Assert.DoesNotContain(chatHistory, message =>
            message.Text == "I found the issue in AuthService.cs and applied a fix.");
    }

    [Fact]
    public async Task PreambleSummary_PreservesRoundsIntact()
    {
        var chatHistory = CreateHistoryWithPreambleAndRounds(shortPreamble: false);
        var summaryClient = new SummaryOnlyLlmClient("previous task summary");
        var summarizer = new Summarizer(new GlobalOptions());
        var strategy = new PreambleSummaryChatHistoryCompressionStrategy(summarizer, new DefaultTokensCounter());

        await strategy.CompressAsync(new ChatHistoryCompressionContext
        {
            ChatHistory = chatHistory,
            Options = new ReactHistoryCompressionOptions
            {
                PreambleCompression = true,
                PreambleTokenThresholdPercent = 0.0001,
            },
            CurrentRound = 0,
            CurrentClient = summaryClient,
        });

        // Round 1 assistant and observation should still be present
        Assert.Contains(chatHistory.SelectMany(message => message.Contents).OfType<FunctionCallContent>(),
            content => content.CallId == "call-current-1");
        Assert.Contains(chatHistory.SelectMany(message => message.Contents).OfType<FunctionResultContent>(),
            content => content.CallId == "call-current-1" && Equals(content.Result, "pipeline config file content"));
    }

    [Fact]
    public async Task PreambleSummary_SkipsWhenNoPreviousTaskMessages()
    {
        // History with only system + user + rounds (no previous task context)
        var chatHistory = CreateHistoryWithTwoRounds();
        var summaryClient = new SummaryOnlyLlmClient("should not be called");
        var summarizer = new Summarizer(new GlobalOptions());
        var strategy = new PreambleSummaryChatHistoryCompressionStrategy(summarizer, new DefaultTokensCounter());

        var originalCount = chatHistory.Count;
        await strategy.CompressAsync(new ChatHistoryCompressionContext
        {
            ChatHistory = chatHistory,
            Options = new ReactHistoryCompressionOptions
            {
                PreambleCompression = true,
                PreambleTokenThresholdPercent = 0,
            },
            CurrentRound = 0,
            CurrentClient = summaryClient,
        });

        // Preamble has system + user = 2 messages, no historical messages in between
        Assert.Equal(originalCount, chatHistory.Count);
    }

    [Fact]
    public async Task LlmClientBase_PreambleSummary_CompressesPreambleBeforeFirstRound()
    {
        // Two responses: first is consumed by the summarizer during preamble compression,
        // second is the actual chat response for the main request.
        var chatClient = new RecordingSequentialChatClient(
            CreateTextResponse("investigated auth module, fixed token refresh bug"),
            CreateTextResponse("done"));
        var client = new CompressionAwareLlmClient(chatClient, ReactHistoryCompressionMode.None);
        client.ModelInfo.HistoryCompression?.PreambleCompression = true;
        client.ModelInfo.HistoryCompression?.PreambleTokenThresholdPercent = 0.0001;

        // Build history: system + previous task messages + current user message
        var chatHistory = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            new(ChatRole.User, "Previous task: fix authentication"),
            new(ChatRole.Assistant,
                "I analyzed the auth module and found a token refresh bug in AuthService.cs. I applied the fix and verified it works."),
            new(ChatRole.User, "Now fix the CI pipeline."),
        };

        var requestContext = new RequestContext
        {
            ChatMessages = chatHistory,
            FunctionCallEngine = new LoopingToolCallEngine(),
            RequestOptions = new ChatOptions(),
        };

#pragma warning disable SKEXP0001
        var result = await client.SendRequestCompatAsync(requestContext, CancellationToken.None);
#pragma warning restore SKEXP0001

        Assert.Null(result.Exception);
        Assert.Equal(2, chatClient.SeenHistories.Count);

        // First history: the summarizer call (contains historical messages for compression)
        // Second history: the main request (should have the compressed preamble)
        var sentHistory = chatClient.SeenHistories[1];
        // Previous task messages should be compressed
        Assert.DoesNotContain("Previous task: fix authentication", sentHistory);
        Assert.DoesNotContain("I analyzed the auth module", sentHistory);
        // Current user message should be preserved
        Assert.Contains("Now fix the CI pipeline", sentHistory);
        // Summary should be present
        Assert.Contains("[Previous task context summary]", sentHistory);
    }

    [Fact]
    public async Task LlmClientBase_PreambleSummary_EmitsCompressionLoopEvents()
    {
        var chatClient = new RecordingSequentialChatClient(
            CreateTextResponse("investigated auth module, fixed token refresh bug"),
            CreateTextResponse("done"));
        var client = new CompressionAwareLlmClient(chatClient, ReactHistoryCompressionMode.None);
        client.ModelInfo.HistoryCompression.PreambleCompression = true;
        client.ModelInfo.HistoryCompression.PreambleTokenThresholdPercent = 0.0001;

        var requestContext = new RequestContext
        {
            ChatMessages =
            [
                new ChatMessage(ChatRole.System, "You are helpful."),
                new ChatMessage(ChatRole.User, "Previous task: fix authentication"),
                new ChatMessage(ChatRole.Assistant,
                    "I analyzed the auth module and found a token refresh bug in AuthService.cs. I applied the fix and verified it works."),
                new ChatMessage(ChatRole.User, "Now fix the CI pipeline."),
            ],
            FunctionCallEngine = new LoopingToolCallEngine(),
            RequestOptions = new ChatOptions(),
        };

#pragma warning disable SKEXP0001
        var stepEvents = await CollectStepEventsAsync(client.SendRequestAsync(requestContext), CancellationToken.None);
#pragma warning restore SKEXP0001

        var firstStepEvents = Assert.Single(stepEvents);
        Assert.Contains(firstStepEvents,
            evt => evt is HistoryCompressionStarted { Kind: HistoryCompressionKind.PreambleSummary });
        Assert.Contains(firstStepEvents,
            evt => evt is HistoryCompressionCompleted
            {
                Kind: HistoryCompressionKind.PreambleSummary,
                Applied: true,
            });
    }

    [Fact]
    public async Task LlmClientBase_RemoveErrorLoop_FiltersErrorPairsFromOlderRoundsBeforeNextRequest()
    {
        // Round 1: LLM returns a tool call, the tool FAILS (engine throws).
        // Round 2: LLM returns a tool call, the tool succeeds.
        // Round 3: LLM returns Stop.
        // PreserveRecentRounds = 1, RemoveErrorLoop = true.
        // History seen by Round 3 request must NOT contain the error pair from round 1.
        var chatClient = new RecordingSequentialChatClient(
            CreateToolCallResponse("call-err", "error value"),
            CreateToolCallResponse("call-ok", "good value"),
            CreateTextResponse("all done"));

        var client = new CompressionAwareLlmClient(chatClient, ReactHistoryCompressionMode.ObservationMasking);
        client.ModelInfo.HistoryCompression?.SummaryErrorLoop = true;
        client.ModelInfo.HistoryCompression?.PreserveRecentRounds = 1;

        var requestContext = new RequestContext
        {
            ChatMessages = [new ChatMessage(ChatRole.User, "do work")],
            FunctionCallEngine = new FailFirstToolCallEngine(),
            RequestOptions = new ChatOptions(),
        };

#pragma warning disable SKEXP0001
        var result = await client.SendRequestCompatAsync(requestContext, CancellationToken.None);
#pragma warning restore SKEXP0001

        Assert.Null(result.Exception);
        Assert.Equal(3, chatClient.SeenHistories.Count);

        // Round 3's request history should not contain the error call or its result.
        var historyForRound3 = chatClient.SeenHistories[2];
        Assert.DoesNotContain("call:noop:call-err", historyForRound3);
        Assert.DoesNotContain("result:call-err", historyForRound3);
        // Round 2's success pair must still be present (it is within PreserveRecentRounds=1).
        Assert.Contains("call-ok", historyForRound3);
    }


    #region Agent Isolation Tests

    [Fact]
    public async Task InfoCleaning_WithAgentId_OnlyCompressesMatchingAgentRounds()
    {
        var chatHistory = CreateMultiAgentHistoryWithTwoRoundsEach();
        var summaryClient = new SummaryOnlyLlmClient("compressed agent-a round 1");
        var summarizer = new Summarizer(new GlobalOptions());
        var strategy = new InfoCleaningChatHistoryCompressionStrategy(summarizer);

        await strategy.CompressAsync(new ChatHistoryCompressionContext
        {
            ChatHistory = chatHistory,
            Options = new ReactHistoryCompressionOptions
            {
                Mode = ReactHistoryCompressionMode.InfoCleaning,
                PreserveRecentRounds = 1,
            },
            CurrentRound = 2,
            CurrentClient = summaryClient,
            AgentId = "Agent-A",
        });

        // Agent-A's round 1 should be summarized
        Assert.Contains(chatHistory, message =>
            message.Role == ChatRole.Assistant &&
            message.Text.Contains("[Round 1 summary]") &&
            message.AdditionalProperties?["llmclient.agent"]?.ToString() == "Agent-A");

        // Agent-B's round 1 should NOT be touched (it's treated as preamble due to agent filter)
        Assert.Contains(chatHistory.SelectMany(message => message.Contents).OfType<FunctionResultContent>(),
            content => content.CallId == "call-b1");

        // Agent-B's round 2 should also remain untouched
        Assert.Contains(chatHistory.SelectMany(message => message.Contents).OfType<FunctionResultContent>(),
            content => content.CallId == "call-b2");
    }

    [Fact]
    public async Task ObservationMasking_WithAgentId_OnlyMasksMatchingAgentObservations()
    {
        var chatHistory = CreateMultiAgentHistoryWithTwoRoundsEach();
        var strategy = new ObservationMaskingChatHistoryCompressionStrategy();

        await strategy.CompressAsync(new ChatHistoryCompressionContext
        {
            ChatHistory = chatHistory,
            Options = new ReactHistoryCompressionOptions
            {
                Mode = ReactHistoryCompressionMode.ObservationMasking,
                PreserveRecentRounds = 1,
                ObservationPlaceholder = "[masked]",
            },
            CurrentRound = 2,
            CurrentClient = new SummaryOnlyLlmClient("unused"),
            AgentId = "Agent-A",
        });

        // Agent-A's old observation should be masked
        Assert.Contains(chatHistory.SelectMany(message => message.Contents).OfType<FunctionResultContent>(),
            content => content.CallId == "call-a1" && Equals(content.Result, "[masked]"));

        // Agent-A's recent observation should NOT be masked
        Assert.Contains(chatHistory.SelectMany(message => message.Contents).OfType<FunctionResultContent>(),
            content => content.CallId == "call-a2" && Equals(content.Result, "agent-a result 2"));

        // Agent-B's observations should NOT be masked (they are preamble when filtered by Agent-A)
        Assert.Contains(chatHistory.SelectMany(message => message.Contents).OfType<FunctionResultContent>(),
            content => content.CallId == "call-b1" && Equals(content.Result, "agent-b result 1"));
    }

    [Fact]
    public async Task TaskSummary_WithAgentId_OnlySummarizesMatchingAgentRounds()
    {
        var chatHistory = CreateMultiAgentHistoryWithTwoRoundsEach();
        var summaryClient = new SummaryOnlyLlmClient("agent-a task summary");
        var summarizer = new Summarizer(new GlobalOptions());
        var strategy = new TaskSummaryChatHistoryCompressionStrategy(summarizer);

        await strategy.CompressAsync(new ChatHistoryCompressionContext
        {
            ChatHistory = chatHistory,
            Options = new ReactHistoryCompressionOptions
            {
                Mode = ReactHistoryCompressionMode.TaskSummary,
                PreserveRecentRounds = 1,
            },
            CurrentRound = 2,
            CurrentClient = summaryClient,
            AgentId = "Agent-A",
        });

        // Should contain the summary with Agent-A's tag
        var summaryMessage = Assert.Single(chatHistory.Where(message =>
            message.Text.Contains("[Compressed history summary]")));
        Assert.Equal("Agent-A", summaryMessage.AdditionalProperties?["llmclient.agent"]?.ToString());

        // Agent-B's round 1 should still be present (treated as preamble)
        Assert.Contains(chatHistory.SelectMany(message => message.Contents).OfType<FunctionResultContent>(),
            content => content.CallId == "call-b1");
    }

    [Fact]
    public async Task PreambleSummary_WithAgentId_TreatsOtherAgentMessagesAsPreamble()
    {
        var chatHistory = CreateMultiAgentHistoryWithPreamble();
        var summaryClient = new SummaryOnlyLlmClient("previous task summary");
        var summarizer = new Summarizer(new GlobalOptions());
        var strategy = new PreambleSummaryChatHistoryCompressionStrategy(summarizer, new DefaultTokensCounter());

        await strategy.CompressAsync(new ChatHistoryCompressionContext
        {
            ChatHistory = chatHistory,
            Options = new ReactHistoryCompressionOptions
            {
                PreambleCompression = true,
                PreambleTokenThresholdPercent = 0.0001,
            },
            CurrentRound = 0,
            CurrentClient = summaryClient,
            AgentId = "Current-Agent",
        });

        // Other agent's messages should be part of preamble and potentially summarized
        // Current agent's system message should be preserved
        Assert.Contains(chatHistory, message =>
            message.Role == ChatRole.System && message.Text == "You are helpful.");

        // Previous agent's messages should be in preamble (not treated as rounds)
        var previousAgentMessages = chatHistory.Where(m =>
            m.AdditionalProperties?["llmclient.agent"]?.ToString() == "Previous-Agent").ToList();
        Assert.All(previousAgentMessages, msg =>
        {
            // These should NOT be in the "rounds' part of any segmentation result
            // since they belong to a different agent
            Assert.True(
                msg.Text.Contains("[Previous task context summary]") || // summarized away
                !msg.Text.StartsWith("[Round"), // or remains as preamble
                $"Message should not be treated as a round: {msg.Text}");
        });
    }

    [Fact]
    public async Task LlmClientBase_AgentId_InitializesRoundNumberFromAgentSpecificHistory()
    {
        // Create history where Agent-A already has round 3 completed
        var chatHistory = new List<ChatMessage>
        {
            new(ChatRole.User, "do work"),
        };

        // Agent-A Round 1, 2, 3 (already executed)
        for (int i = 1; i <= 3; i++)
        {
            var assistant = CreateToolCallResponse($"call-a{i}", $"result-a{i}").Messages[0];
            ChatMessageHierarchy.TagLoopLevel(assistant, i, ReactHistoryMessageKind.Assistant, "Agent-A");
            chatHistory.Add(assistant);

            var obs = new ChatMessage(ChatRole.Tool, [new FunctionResultContent($"call-a{i}", $"result-a{i}")]);
            ChatMessageHierarchy.TagLoopLevel(obs, i, ReactHistoryMessageKind.Observation, "Agent-A");
            chatHistory.Add(obs);
        }

        // Agent-B Round 1 (also in history - should be ignored for Agent-A)
        var bAssistant = CreateToolCallResponse("call-b1", "result-b1").Messages[0];
        ChatMessageHierarchy.TagLoopLevel(bAssistant, 1, ReactHistoryMessageKind.Assistant, "Agent-B");
        chatHistory.Add(bAssistant);

        var bObs = new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-b1", "result-b1")]);
        ChatMessageHierarchy.TagLoopLevel(bObs, 1, ReactHistoryMessageKind.Observation, "Agent-B");
        chatHistory.Add(bObs);

        var chatClient = new RecordingSequentialChatClient(
            CreateTextResponse("done"));
        var client = new CompressionAwareLlmClient(chatClient, ReactHistoryCompressionMode.None);

        var requestContext = new RequestContext
        {
            ChatMessages = chatHistory,
            FunctionCallEngine = new LoopingToolCallEngine(),
            RequestOptions = new ChatOptions(),
            AgentId = "Agent-A",
        };

#pragma warning disable SKEXP0001
        var result = await client.SendRequestCompatAsync(requestContext, CancellationToken.None);
#pragma warning restore SKEXP0001

        Assert.Null(result.Exception);
        // The request should start from round 4 (3 existing + 1 new)
        // Verify by checking the tagged messages in the result
        var agentATaggedMessages = result.Messages.Where(m =>
            m.AdditionalProperties?["llmclient.agent"]?.ToString() == "Agent-A").ToList();
        Assert.NotEmpty(agentATaggedMessages);

        // The new assistant message should have round number 4
        var newAssistantMessage = agentATaggedMessages.FirstOrDefault(m => m.Role == ChatRole.Assistant);
        Assert.NotNull(newAssistantMessage);
        Assert.Equal(4, newAssistantMessage.AdditionalProperties?["llmclient.react.round"]);
    }

    [Fact]
    public async Task ReactAgentBase_Execute_SetsAgentIdOnRequestContext()
    {
        var chatClient = new SummaryOnlyLlmClient("done");
        var agent = new TestableReactAgent(chatClient, "MyTestAgent");

        var session = new MockDialogSession();
        await foreach (var step in agent.Execute(session))
        {
            // Just consume
        }

        Assert.Equal("MyTestAgent", agent.LastRequestContext?.AgentId);
    }

    [Fact]
    public void ReactAgentBase_AgentId_DefaultsToName()
    {
        var chatClient = new SummaryOnlyLlmClient("done");
        var agent = new TestableReactAgent(chatClient, "DefaultNameAgent");

        Assert.Equal("DefaultNameAgent", agent.ExposedAgentId);
    }

    private static List<ChatMessage> CreateMultiAgentHistoryWithTwoRoundsEach()
    {
        var history = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            new(ChatRole.User, "Start task"),
        };

        // Agent-A Round 1
        var aAssistant1 = new ChatMessage(ChatRole.Assistant, [
            new TextContent("Agent-A reasoning 1"),
            new FunctionCallContent("call-a1", "noop", new Dictionary<string, object?>())
        ]);
        ChatMessageHierarchy.TagLoopLevel(aAssistant1, 1, ReactHistoryMessageKind.Assistant, "Agent-A");
        history.Add(aAssistant1);

        var aObs1 = new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-a1", "agent-a result 1")]);
        ChatMessageHierarchy.TagLoopLevel(aObs1, 1, ReactHistoryMessageKind.Observation, "Agent-A");
        history.Add(aObs1);

        // Agent-B Round 1
        var bAssistant1 = new ChatMessage(ChatRole.Assistant, [
            new TextContent("Agent-B reasoning 1"),
            new FunctionCallContent("call-b1", "noop", new Dictionary<string, object?>())
        ]);
        ChatMessageHierarchy.TagLoopLevel(bAssistant1, 1, ReactHistoryMessageKind.Assistant, "Agent-B");
        history.Add(bAssistant1);

        var bObs1 = new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-b1", "agent-b result 1")]);
        ChatMessageHierarchy.TagLoopLevel(bObs1, 1, ReactHistoryMessageKind.Observation, "Agent-B");
        history.Add(bObs1);

        // Agent-A Round 2
        var aAssistant2 = new ChatMessage(ChatRole.Assistant, [
            new TextContent("Agent-A reasoning 2"),
            new FunctionCallContent("call-a2", "noop", new Dictionary<string, object?>())
        ]);
        ChatMessageHierarchy.TagLoopLevel(aAssistant2, 2, ReactHistoryMessageKind.Assistant, "Agent-A");
        history.Add(aAssistant2);

        var aObs2 = new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-a2", "agent-a result 2")]);
        ChatMessageHierarchy.TagLoopLevel(aObs2, 2, ReactHistoryMessageKind.Observation, "Agent-A");
        history.Add(aObs2);

        // Agent-B Round 2
        var bAssistant2 = new ChatMessage(ChatRole.Assistant, [
            new TextContent("Agent-B reasoning 2"),
            new FunctionCallContent("call-b2", "noop", new Dictionary<string, object?>())
        ]);
        ChatMessageHierarchy.TagLoopLevel(bAssistant2, 2, ReactHistoryMessageKind.Assistant, "Agent-B");
        history.Add(bAssistant2);

        var bObs2 = new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-b2", "agent-b result 2")]);
        ChatMessageHierarchy.TagLoopLevel(bObs2, 2, ReactHistoryMessageKind.Observation, "Agent-B");
        history.Add(bObs2);

        return history;
    }

    private static List<ChatMessage> CreateMultiAgentHistoryWithPreamble()
    {
        var history = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            // Previous agent's task (untagged, simulating old history)
            new(ChatRole.User, "Previous task"),
            new(ChatRole.Assistant, "Previous agent response"),
        };

        // Previous-Agent Round 1
        var prevAssistant = new ChatMessage(ChatRole.Assistant, [
            new FunctionCallContent("call-prev", "noop", new Dictionary<string, object?>())
        ]);
        ChatMessageHierarchy.TagLoopLevel(prevAssistant, 1, ReactHistoryMessageKind.Assistant, "Previous-Agent");
        history.Add(prevAssistant);

        var prevObs = new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-prev", "prev result")]);
        ChatMessageHierarchy.TagLoopLevel(prevObs, 1, ReactHistoryMessageKind.Observation, "Previous-Agent");
        history.Add(prevObs);

        // Current task user message
        history.Add(new ChatMessage(ChatRole.User, "Current task"));

        // Current-Agent Round 1
        var currAssistant = new ChatMessage(ChatRole.Assistant, [
            new FunctionCallContent("call-curr", "noop", new Dictionary<string, object?>())
        ]);
        ChatMessageHierarchy.TagLoopLevel(currAssistant, 1, ReactHistoryMessageKind.Assistant, "Current-Agent");
        history.Add(currAssistant);

        var currObs = new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-curr", "curr result")]);
        ChatMessageHierarchy.TagLoopLevel(currObs, 1, ReactHistoryMessageKind.Observation, "Current-Agent");
        history.Add(currObs);

        return history;
    }

    private sealed class TestableReactAgent : ReactAgentBase
    {
        private readonly string _name;

        public TestableReactAgent(ILLMChatClient chatClient, string name)
            : base(chatClient, new AgentOption(), MiniSweAgentConfigLoader.LoadDefaultWindowsConfig())
        {
            _name = name;
        }

        public override string Name => _name;
        public string ExposedAgentId => AgentId;
        public RequestContext? LastRequestContext { get; private set; }

        protected override Task<RequestContext?> BuildRequestContextAsync(
            ITextDialogSession dialogSession,
            CancellationToken cancellationToken)
        {
            var context = new RequestContext
            {
                ChatMessages = new List<ChatMessage> { new(ChatRole.User, "test") },
                FunctionCallEngine = new LoopingToolCallEngine(),
                RequestOptions = new ChatOptions(),
            };
            LastRequestContext = context;
            return Task.FromResult<RequestContext?>(context);
        }
    }

    private sealed class MockDialogSession : ITextDialogSession
    {
        public Guid ID { get; } = Guid.NewGuid();
        public IReadOnlyList<IDialogItem> DialogItems { get; } = new List<IDialogItem>();
        public List<IChatHistoryItem> GetHistory() => [];
        public Task CutContextAsync(IRequestItem? requestItem = null) => Task.CompletedTask;
        
        public AIContextProvider[]? ContextProviders { get; } = null;
        public string? SystemPrompt { get; } = null;
        public IEnumerable<Type> SupportedAgents { get; } = Array.Empty<Type>();
        public IFunctionGroupSource? ToolsSource { get; } = null;

        public Task<IResponse> NewResponse(RequestOption option, IRequestItem? insertBefore = null,
            CancellationToken token = default)
            => Task.FromResult<IResponse>(new RawResponseViewItem());
    }

    #endregion

    [Fact]
    public async Task ObservationMasking_MasksOlderRoundObservation_AndPreservesRecentRoundObservation()
    {
        // Round 1: old success round (should be masked).
        // Round 2: recent error round (should be preserved as-is 锟斤拷 strategy does not process errors).
        var chatHistory = CreateHistoryWithRecentErrorRound();
        var strategy = new ObservationMaskingChatHistoryCompressionStrategy();

        await strategy.CompressAsync(new ChatHistoryCompressionContext
        {
            ChatHistory = chatHistory,
            Options = new ReactHistoryCompressionOptions
            {
                Mode = ReactHistoryCompressionMode.ObservationMasking,
                PreserveRecentRounds = 1,
                ObservationPlaceholder = "[details omitted for brevity]",
            },
            CurrentRound = 2,
            CurrentClient = new SummaryOnlyLlmClient("unused"),
        });

        // Old round (round 1) observation should be masked with placeholder
        Assert.Contains(chatHistory.SelectMany(message => message.Contents).OfType<FunctionResultContent>(),
            content => content.CallId == "call-ok-old" && Equals(content.Result, "[details omitted for brevity]"));
        // Recent error round (round 2) observation must be preserved unchanged (strategy does not summarize errors)
        Assert.DoesNotContain(chatHistory, message =>
            message.Text.StartsWith("[Round 2 error summary]", StringComparison.Ordinal));
        Assert.Contains(chatHistory.SelectMany(message => message.Contents).OfType<FunctionResultContent>(),
            content => content.CallId == "call-err-recent" && Equals(content.Result, "recent error payload"));
    }

    [Fact]
    public async Task LlmClientBase_ModeNoneWithSummaryErrorLoop_CompressesOlderErrorRound()
    {
        var chatClient = new RecordingSequentialChatClient(
            CreateToolCallResponse("call-err", "error value"),
            CreateToolCallResponse("call-ok", "good value"),
            CreateTextResponse("all done"));
        var client = new CompressionAwareLlmClient(chatClient, ReactHistoryCompressionMode.None);
        client.ModelInfo.HistoryCompression?.SummaryErrorLoop = true;
        client.ModelInfo.HistoryCompression?.PreserveRecentRounds = 1;

        var requestContext = new RequestContext
        {
            ChatMessages = [new ChatMessage(ChatRole.User, "do work")],
            FunctionCallEngine = new FailFirstToolCallEngine(),
            RequestOptions = new ChatOptions(),
        };

#pragma warning disable SKEXP0001
        var result = await client.SendRequestCompatAsync(requestContext, CancellationToken.None);
#pragma warning restore SKEXP0001

        Assert.Null(result.Exception);
        Assert.Equal(3, chatClient.SeenHistories.Count);
        var historyForRound3 = chatClient.SeenHistories[2];
        Assert.DoesNotContain("call:noop:call-err", historyForRound3);
        Assert.Contains("error summary", historyForRound3);
    }

    private static List<ChatMessage> CreateHistoryWithErrorAndSuccessRounds()
    {
        var history = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            new(ChatRole.User, "Fix the failing workflow."),
        };

        // Round 1: one error call + one success call.
        var assistantRound1 = new ChatMessage(ChatRole.Assistant,
        [
            new TextContent("Inspecting."),
            new FunctionCallContent("call-err-1", "broken_tool", new Dictionary<string, object?>()),
            new FunctionCallContent("call-ok-1", "working_tool", new Dictionary<string, object?>()),
        ]);
        ChatMessageHierarchy.TagLoopLevel(assistantRound1, 1, ReactHistoryMessageKind.Assistant);
        history.Add(assistantRound1);

        var observationRound1 = new ChatMessage(ChatRole.Tool,
        [
            new FunctionResultContent("call-err-1", "error message") { Exception = new Exception("tool error") },
            new FunctionResultContent("call-ok-1", "file content"),
        ]);
        ChatMessageHierarchy.TagLoopLevel(observationRound1, 1, ReactHistoryMessageKind.Observation);
        history.Add(observationRound1);

        // Round 2 (preserved): normal success call.
        var assistantRound2 = new ChatMessage(ChatRole.Assistant,
        [
            new TextContent("Applying fix."),
            new FunctionCallContent("call-ok-2", "edit_file", new Dictionary<string, object?>()),
        ]);
        ChatMessageHierarchy.TagLoopLevel(assistantRound2, 2, ReactHistoryMessageKind.Assistant);
        history.Add(assistantRound2);

        var observationRound2 = new ChatMessage(ChatRole.Tool,
        [
            new FunctionResultContent("call-ok-2", "edit applied"),
        ]);
        ChatMessageHierarchy.TagLoopLevel(observationRound2, 2, ReactHistoryMessageKind.Observation);
        history.Add(observationRound2);

        return history;
    }

    private static List<ChatMessage> CreateHistoryWithRecentErrorRound()
    {
        var history = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            new(ChatRole.User, "Fix the failing workflow."),
        };

        var assistantRound1 = new ChatMessage(ChatRole.Assistant,
        [
            new FunctionCallContent("call-ok-old", "old_tool", new Dictionary<string, object?>()),
        ]);
        ChatMessageHierarchy.TagLoopLevel(assistantRound1, 1, ReactHistoryMessageKind.Assistant);
        history.Add(assistantRound1);

        var observationRound1 = new ChatMessage(ChatRole.Tool,
        [
            new FunctionResultContent("call-ok-old", "old success payload"),
        ]);
        ChatMessageHierarchy.TagLoopLevel(observationRound1, 1, ReactHistoryMessageKind.Observation);
        history.Add(observationRound1);

        var assistantRound2 = new ChatMessage(ChatRole.Assistant,
        [
            new FunctionCallContent("call-err-recent", "recent_tool", new Dictionary<string, object?>()),
        ]);
        ChatMessageHierarchy.TagLoopLevel(assistantRound2, 2, ReactHistoryMessageKind.Assistant);
        history.Add(assistantRound2);

        var observationRound2 = new ChatMessage(ChatRole.Tool,
        [
            new FunctionResultContent("call-err-recent", "recent error payload")
            {
                Exception = new InvalidOperationException("recent tool failure"),
            },
        ]);
        ChatMessageHierarchy.TagLoopLevel(observationRound2, 2, ReactHistoryMessageKind.Observation);
        history.Add(observationRound2);

        return history;
    }

    [Fact]
    public async Task LlmClientBase_ObservationMasking_EmitsCompressionLoopEvents()
    {
        var chatClient = new RecordingSequentialChatClient(
            CreateToolCallResponse("call-1", "first observation"),
            CreateToolCallResponse("call-2", "second observation"),
            CreateTextResponse("done"));
        var client = new CompressionAwareLlmClient(chatClient, ReactHistoryCompressionMode.ObservationMasking);
        client.ModelInfo.HistoryCompression.PreserveRecentRounds = 1;
        client.ModelInfo.HistoryCompression.ReactTokenThresholdPercent = 0.0001; // very low to always trigger

        var requestContext = new RequestContext
        {
            ChatMessages = [new ChatMessage(ChatRole.User, "solve the issue")],
            FunctionCallEngine = new LoopingToolCallEngine(),
            RequestOptions = new ChatOptions(),
        };

#pragma warning disable SKEXP0001
        var stepEvents = await CollectStepEventsAsync(client.SendRequestAsync(requestContext), CancellationToken.None);
#pragma warning restore SKEXP0001

        Assert.Equal(3, stepEvents.Count);
        Assert.DoesNotContain(stepEvents[0], evt => evt is HistoryCompressionStarted or HistoryCompressionCompleted);
        Assert.Contains(stepEvents[1],
            evt => evt is HistoryCompressionStarted { Kind: HistoryCompressionKind.ObservationMasking });
        Assert.Contains(stepEvents[1],
            evt => evt is HistoryCompressionCompleted
            {
                Kind: HistoryCompressionKind.ObservationMasking,
                Applied: true,
            });
    }

    [Fact]
    public async Task ResponseViewItemBase_RendersHistoryCompressionEventsIntoLoopBuffer()
    {
        var response = new RawResponseViewItem();

        await response.ConsumeReactStepsAsync(CreateCompressionEventSteps());

        var loops = response.Loops;

        var loop = Assert.Single(loops);
        var renderedText = string.Concat(loop.ResponseBuffer);
        Assert.Contains("History compression: previous task context summary started.", renderedText);
        Assert.Contains("History compression: previous task context summary applied.", renderedText);
        Assert.Equal("History compression: previous task context summary started.", loop.FirstLine);
    }

    private static List<ChatMessage> CreateHistoryWithPreambleAndRounds(bool shortPreamble)
    {
        var history = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
        };

        if (!shortPreamble)
        {
            // Previous task messages (no round tags 锟斤拷 simulating persisted history from prior task)
            history.Add(new ChatMessage(ChatRole.User, "Fix the authentication bug."));
            history.Add(new ChatMessage(ChatRole.Assistant,
                "I found the issue in AuthService.cs and applied a fix. The token refresh logic was incorrectly handling expired tokens."));
            history.Add(new ChatMessage(ChatRole.User, "Good. Can you also verify the tests pass?"));
            history.Add(new ChatMessage(ChatRole.Assistant,
                "All 47 tests pass. The authentication module is now working correctly with proper token lifecycle management."));
        }

        // Current task user message
        history.Add(new ChatMessage(ChatRole.User, "Now fix the CI pipeline."));

        // Current task round 1 (with round tags)
        var assistantRound1 = new ChatMessage(ChatRole.Assistant,
        [
            new TextContent("I will read the pipeline config."),
            new FunctionCallContent("call-current-1", "read_file", new Dictionary<string, object?>())
        ]);
        ChatMessageHierarchy.TagLoopLevel(assistantRound1, 1, ReactHistoryMessageKind.Assistant);
        history.Add(assistantRound1);

        var observationRound1 = new ChatMessage(ChatRole.Tool,
        [
            new FunctionResultContent("call-current-1", "pipeline config file content")
        ]);
        ChatMessageHierarchy.TagLoopLevel(observationRound1, 1, ReactHistoryMessageKind.Observation);
        history.Add(observationRound1);

        return history;
    }

    private static List<ChatMessage> CreateHistoryWithTwoRounds()
    {
        var history = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            new(ChatRole.User, "Fix the failing workflow."),
        };

        var assistantRound1 = new ChatMessage(ChatRole.Assistant,
        [
            new TextReasoningContent("Need to inspect repository state."),
            new TextContent("I will inspect the repository."),
            new FunctionCallContent("call-1", "read_file", new Dictionary<string, object?>())
        ]);
        ChatMessageHierarchy.TagLoopLevel(assistantRound1, 1, ReactHistoryMessageKind.Assistant);
        history.Add(assistantRound1);

        var observationRound1 = new ChatMessage(ChatRole.Tool,
        [
            new FunctionResultContent("call-1", "first observation")
        ]);
        ChatMessageHierarchy.TagLoopLevel(observationRound1, 1, ReactHistoryMessageKind.Observation);
        history.Add(observationRound1);

        var assistantRound2 = new ChatMessage(ChatRole.Assistant,
        [
            new TextReasoningContent("Need another file check."),
            new TextContent("I will inspect one more file."),
            new FunctionCallContent("call-2", "read_file", new Dictionary<string, object?>())
        ]);
        ChatMessageHierarchy.TagLoopLevel(assistantRound2, 2, ReactHistoryMessageKind.Assistant);
        history.Add(assistantRound2);

        var observationRound2 = new ChatMessage(ChatRole.Tool,
        [
            new FunctionResultContent("call-2", "second observation")
        ]);
        ChatMessageHierarchy.TagLoopLevel(observationRound2, 2, ReactHistoryMessageKind.Observation);
        history.Add(observationRound2);

        return history;
    }

    private static ChatResponse CreateToolCallResponse(string callId, string observation, string? reasoning = null)
    {
        var contents = new List<AIContent>();
        if (!string.IsNullOrWhiteSpace(reasoning))
        {
            contents.Add(new TextReasoningContent(reasoning));
        }

        contents.Add(new FunctionCallContent(callId, "noop", new Dictionary<string, object?>
        {
            ["value"] = observation,
        }));

        var message = new ChatMessage(ChatRole.Assistant, contents);

        return new ChatResponse([message])
        {
            FinishReason = ChatFinishReason.ToolCalls,
            RawRepresentation = observation,
        };
    }

    private static ChatResponse CreateTextResponse(string text)
    {
        return new ChatResponse([new ChatMessage(ChatRole.Assistant, text)])
        {
            FinishReason = ChatFinishReason.Stop,
        };
    }

    private static async Task<List<List<LoopEvent>>> CollectStepEventsAsync(
        IAsyncEnumerable<ReactStep> steps,
        CancellationToken cancellationToken)
    {
        var results = new List<List<LoopEvent>>();
        await foreach (var step in steps.WithCancellation(cancellationToken))
        {
            var events = new List<LoopEvent>();
            await foreach (var evt in step.WithCancellation(cancellationToken))
            {
                events.Add(evt);
            }

            results.Add(events);
        }

        return results;
    }

    private static int CountOccurrences(string source, string value)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
        {
            return 0;
        }

        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static async IAsyncEnumerable<ReactStep> CreateCompressionEventSteps()
    {
        var step = new ReactStep();
        step.EmitHistoryCompressionStarted(HistoryCompressionKind.PreambleSummary);
        step.EmitHistoryCompressionCompleted(HistoryCompressionKind.PreambleSummary, true);
        step.Complete(new StepResult
        {
            FinishReason = ChatFinishReason.Stop,
            IsCompleted = true,
            Messages = [],
        });
        yield return step;
        await Task.CompletedTask;
    }

    private sealed class CompressionAwareLlmClient : LlmClientBase
    {
        private readonly IChatClient _chatClient;

        public CompressionAwareLlmClient(IChatClient chatClient, ReactHistoryCompressionMode mode,
            ITokensCounter? tokensCounter = null) : base(tokensCounter ?? new DefaultTokensCounter())
        {
            _chatClient = chatClient;
            ModelInfo = new APIModelInfo
            {
                APIId = "compression-test-model",
                Name = "Compression Test Model",
                Endpoint = EmptyLLMEndpoint.Instance,
                SupportFunctionCall = true,
                SupportStreaming = false,
                SupportSystemPrompt = true,
                FunctionCallOnStreaming = false,
                SupportTextGeneration = true,
                HistoryCompression = new ReactHistoryCompressionOptions
                {
                    Mode = mode,
                    PreserveRecentRounds = 1,
                },
            };
            Parameters = new DefaultModelParam
            {
                Streaming = false,
            };
        }

        public APIModelInfo ModelInfo { get; }

        public override string Name => "CompressionAwareLlmClient";

        public override ILLMAPIEndpoint Endpoint => EmptyLLMEndpoint.Instance;

        public override IEndpointModel Model => ModelInfo;

        protected override IChatClient GetChatClient()
        {
            return _chatClient;
        }
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

        public IModelParams Parameters { get; set; } = new DefaultModelParam
        {
            Streaming = false,
        };

        public bool IsResponding { get; set; }

        public async IAsyncEnumerable<ReactStep> SendRequestAsync(IRequestContext requestContext,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var step = new ReactStep();
            step.EmitText(_summaryText);
            var message = new ChatMessage(ChatRole.Assistant, _summaryText);
            step.Complete(new StepResult
            {
                FinishReason = ChatFinishReason.Stop,
                IsCompleted = true,
                Messages = [message],
            });
            yield return step;
            await Task.CompletedTask;
        }
    }

    private sealed class RecordingSequentialChatClient(params ChatResponse[] responses) : IChatClient
    {
        private readonly Queue<ChatResponse> _responses = new(responses);

        public List<string> SeenHistories { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options,
            CancellationToken cancellationToken = default)
        {
            SeenHistories.Add(RenderHistory(chatMessages));
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No more responses configured.");
            }

            return Task.FromResult(_responses.Dequeue());
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public object? GetService(Type serviceType, object? serviceKey)
        {
            return null;
        }

        public void Dispose()
        {
        }

        private static string RenderHistory(IEnumerable<ChatMessage> chatMessages)
        {
            var parts = new List<string>();
            foreach (var message in chatMessages)
            {
                foreach (var content in message.Contents)
                {
                    switch (content)
                    {
                        case TextContent textContent:
                            parts.Add($"{message.Role}:text:{textContent.Text}");
                            break;
                        case TextReasoningContent reasoningContent:
                            parts.Add($"{message.Role}:reasoning:{reasoningContent.Text}");
                            break;
                        case FunctionCallContent functionCallContent:
                            parts.Add($"{message.Role}:call:{functionCallContent.Name}:{functionCallContent.CallId}");
                            break;
                        case FunctionResultContent functionResultContent:
                            parts.Add(
                                $"{message.Role}:result:{functionResultContent.CallId}:{functionResultContent.Result}");
                            break;
                    }
                }
            }

            return string.Join("\n", parts);
        }
    }

    private sealed class LoopingToolCallEngine : FunctionCallEngine
    {
        public LoopingToolCallEngine()
        {
            KernelPluginCollection.AddFromFunctions("Test",
            [
                KernelFunctionFactory.CreateFromMethod(
                    (string value) => value,
                    new KernelFunctionFromMethodOptions { FunctionName = "noop" })
            ]);
        }

        public override bool IsToolCallMode => false;

        public override void PreviewRequest(ChatOptions options, IEndpointModel model, IList<ChatMessage> chatMessages)
        {
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

    /// <summary>
    /// A tool call engine whose "noop" function throws on the very first invocation,
    /// producing a <see cref="FunctionResultContent"/> with a non-null
    /// <see cref="FunctionResultContent.Exception"/>.  All subsequent calls succeed.
    /// </summary>
    private sealed class FailFirstToolCallEngine : FunctionCallEngine
    {
        private int _callCount;

        public FailFirstToolCallEngine()
        {
            KernelPluginCollection.AddFromFunctions("Test",
            [
                KernelFunctionFactory.CreateFromMethod(
                    (string value) =>
                    {
                        if (Interlocked.Increment(ref _callCount) == 1)
                        {
                            throw new InvalidOperationException("Simulated tool failure");
                        }

                        return value;
                    },
                    new KernelFunctionFromMethodOptions { FunctionName = "noop" })
            ]);
        }

        public override bool IsToolCallMode => true;

        public override void PreviewRequest(ChatOptions options, IEndpointModel model, IList<ChatMessage> chatMessages)
        {
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
}