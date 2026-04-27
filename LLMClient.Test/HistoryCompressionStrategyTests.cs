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
            .AddSingleton<ErrorSummaryChatHistoryCompressionStrategy>()
            .AddSingleton<ChatHistoryCompressionStrategyFactory>()
            .BuildServiceProvider();
        BaseViewModel.ServiceLocator = services;
    }

    #region ObservationMasking Strategy Tests

    [Fact]
    public async Task ObservationMasking_ReplacesOnlyOlderRoundObservations()
    {
        var history = CreateSegmentedHistoryWithTwoRounds();
        var strategy = new ObservationMaskingChatHistoryCompressionStrategy();

        await strategy.CompressAsync(new ChatHistoryContext
        {
            History = history,
            Options = new ReactHistoryCompressionOptions
            {
                Mode = ReactHistoryCompressionMode.ObservationMasking,
                PreserveRecentRounds = 1,
                ObservationPlaceholder = "[details omitted for brevity]",
            },
            CurrentRoundNumber = 2,
            CurrentClient = new SummaryOnlyLlmClient("unused"),
        });

        // Round 1 observation should be masked
        var round1Obs = history.Rounds[0].ObservationMessage;
        Assert.NotNull(round1Obs);
        Assert.Contains(round1Obs.Contents.OfType<FunctionResultContent>(),
            content => Equals(content.Result, "[details omitted for brevity]") && content.CallId == "call-1");

        // Round 2 observation should NOT be masked
        var round2Obs = history.Rounds[1].ObservationMessage;
        Assert.NotNull(round2Obs);
        Assert.Contains(round2Obs.Contents.OfType<FunctionResultContent>(),
            content => Equals(content.Result, "second observation") && content.CallId == "call-2");

        // Original "first observation" should no longer exist
        Assert.DoesNotContain(round1Obs.Contents.OfType<FunctionResultContent>(),
            content => Equals(content.Result, "first observation"));
    }

    [Fact]
    public async Task ObservationMasking_MasksOlderRoundObservation_AndPreservesRecentRoundObservation()
    {
        var history = CreateSegmentedHistoryWithRecentErrorRound();
        var strategy = new ObservationMaskingChatHistoryCompressionStrategy();

        await strategy.CompressAsync(new ChatHistoryContext
        {
            History = history,
            Options = new ReactHistoryCompressionOptions
            {
                Mode = ReactHistoryCompressionMode.ObservationMasking,
                PreserveRecentRounds = 1,
                ObservationPlaceholder = "[details omitted for brevity]",
            },
            CurrentRoundNumber = 2,
            CurrentClient = new SummaryOnlyLlmClient("unused"),
        });

        // Old round observation should be masked with placeholder
        var oldObs = history.Rounds[0].ObservationMessage;
        Assert.NotNull(oldObs);
        Assert.Contains(oldObs.Contents.OfType<FunctionResultContent>(),
            content => content.CallId == "call-ok-old" && Equals(content.Result, "[details omitted for brevity]"));

        // Recent error round observation must be preserved unchanged
        var recentObs = history.Rounds[1].ObservationMessage;
        Assert.NotNull(recentObs);
        Assert.Contains(recentObs.Contents.OfType<FunctionResultContent>(),
            content => content.CallId == "call-err-recent" && Equals(content.Result, "recent error payload"));
    }

    #endregion

    #region LoopSummary Strategy Tests

    [Fact]
    public async Task InfoCleaning_ReplacesOlderRoundWithAssistantSummary()
    {
        var history = CreateSegmentedHistoryWithTwoRounds();
        var summaryClient = new SummaryOnlyLlmClient("checked repository state and captured first observation");
        var summarizer = new Summarizer(new GlobalOptions());
        var strategy = new LoopSummaryChatHistoryCompressionStrategy(summarizer);

        await strategy.CompressAsync(new ChatHistoryContext
        {
            History = history,
            Options = new ReactHistoryCompressionOptions
            {
                Mode = ReactHistoryCompressionMode.LoopSummary,
                PreserveRecentRounds = 1,
            },
            CurrentRoundNumber = 2,
            CurrentClient = summaryClient,
        });

        // Round 1 should be summarized (assistant message replaced)
        var round1 = history.Rounds[0];
        Assert.NotNull(round1.AssistantMessage);
        Assert.StartsWith("[Round 1 summary]", round1.AssistantMessage.Text, StringComparison.Ordinal);
        Assert.Contains("checked repository state and captured first observation", round1.AssistantMessage.Text);

        // Round 1's original function call content should be gone
        Assert.Empty(round1.AssistantMessage.Contents.OfType<FunctionCallContent>());

        // Round 2 should be untouched
        var round2 = history.Rounds[1];
        Assert.NotNull(round2.AssistantMessage);
        Assert.Contains(round2.AssistantMessage.Contents.OfType<FunctionCallContent>(),
            content => content.CallId == "call-2");
    }

    #endregion

    #region TaskSummary Strategy Tests

    [Fact]
    public async Task TaskSummary_UsesSummarizerToCollapseEarlierRounds()
    {
        var history = CreateSegmentedHistoryWithTwoRounds();
        var summaryClient = new SummaryOnlyLlmClient("condensed task summary");
        var summarizer = new Summarizer(new GlobalOptions());
        var strategy = new TaskSummaryChatHistoryCompressionStrategy(summarizer);

        await strategy.CompressAsync(new ChatHistoryContext
        {
            History = history,
            Options = new ReactHistoryCompressionOptions
            {
                Mode = ReactHistoryCompressionMode.TaskSummary,
                PreserveRecentRounds = 1,
            },
            CurrentRoundNumber = 2,
            CurrentClient = summaryClient,
        });

        // First round should be collapsed into a summary round
        Assert.NotEmpty(history.Rounds);
        var summaryRound = history.Rounds[0];
        Assert.NotNull(summaryRound.AssistantMessage);
        Assert.Contains("[Compressed history summary]", summaryRound.AssistantMessage.Text);
        Assert.Contains("condensed task summary", summaryRound.AssistantMessage.Text);

        // The original round 1 should be gone (replaced by summary round)
        Assert.DoesNotContain(history.Rounds,
            round => round.AssistantMessage?.Contents.OfType<FunctionCallContent>()
                .Any(c => c.CallId == "call-1") == true);

        // Round 2 should be preserved
        Assert.Contains(history.Rounds,
            round => round.AssistantMessage?.Contents.OfType<FunctionCallContent>()
                .Any(c => c.CallId == "call-2") == true);
    }

    #endregion

    #region LlmClientBase Integration Tests

    [Fact]
    public async Task LlmClientBase_ObservationMasking_CompressesHistoryBeforeNextLoop()
    {
        var chatClient = new RecordingSequentialChatClient(
            CreateToolCallResponse("call-1", "first observation"),
            CreateToolCallResponse("call-2", "second observation"),
            CreateTextResponse("done"));
        var client = new CompressionAwareLlmClient(chatClient, ReactHistoryCompressionMode.ObservationMasking);
        client.ModelInfo.HistoryCompression!.PreserveRecentRounds = 1;
        client.ModelInfo.HistoryCompression.ObservationPlaceholder = "[details omitted for brevity]";
        client.ModelInfo.HistoryCompression.ReactTokenThresholdPercent = 0.0001; // very low to always trigger
        var requestContext = new RequestContext
        {
            ChatMessages = [new ChatMessage(ChatRole.User, "solve the issue")],
            FunctionCallEngine = new LoopingToolCallEngine(),
            RequestOptions = new ChatOptions(),
            DialogId = "test-dialog",
        };

#pragma warning disable SKEXP0001
        var result = await ConsumeAllStepsAsync(client.SendRequestAsync(requestContext), CancellationToken.None);
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
            DialogId = "test-dialog",
        };

#pragma warning disable SKEXP0001
        var result = await ConsumeAllStepsAsync(client.SendRequestAsync(requestContext), CancellationToken.None);
#pragma warning restore SKEXP0001

        Assert.Null(result.Exception);
        Assert.Equal(3, chatClient.SeenHistories.Count);
        Assert.Equal(1, CountOccurrences(chatClient.SeenHistories[2], $"assistant:reasoning:{repeatedThinking}"));
    }

    [Fact]
    public async Task LlmClientBase_ObservationMasking_EmitsCompressionLoopEvents()
    {
        var chatClient = new RecordingSequentialChatClient(
            CreateToolCallResponse("call-1", "first observation"),
            CreateToolCallResponse("call-2", "second observation"),
            CreateTextResponse("done"));
        var client = new CompressionAwareLlmClient(chatClient, ReactHistoryCompressionMode.ObservationMasking);
        client.ModelInfo.HistoryCompression!.PreserveRecentRounds = 1;
        client.ModelInfo.HistoryCompression.ReactTokenThresholdPercent = 0.0001; // very low to always trigger

        var requestContext = new RequestContext
        {
            ChatMessages = [new ChatMessage(ChatRole.User, "solve the issue")],
            FunctionCallEngine = new LoopingToolCallEngine(),
            RequestOptions = new ChatOptions(),
            DialogId = "test-dialog",
        };

#pragma warning disable SKEXP0001
        var stepEvents = await CollectStepEventsAsync(client.SendRequestAsync(requestContext), CancellationToken.None);
#pragma warning restore SKEXP0001

        Assert.Equal(3, stepEvents.Count);
        // Compression events are emitted during tool-call rounds regardless of whether
        // actual data was compressed; verify presence of events in the react loop.
        var allEvents = stepEvents.SelectMany(e => e).ToList();
        Assert.Contains(allEvents,
            evt => evt is HistoryCompressionStarted { Kind: HistoryCompressionKind.ObservationMasking });
        Assert.Contains(allEvents,
            evt => evt is HistoryCompressionCompleted
            {
                Kind: HistoryCompressionKind.ObservationMasking,
            });
    }

    [Fact]
    public async Task LlmClientBase_RemoveErrorLoop_FiltersErrorPairsFromOlderRoundsBeforeNextRequest()
    {
        var chatClient = new RecordingSequentialChatClient(
            CreateToolCallResponse("call-err", "error value"),
            CreateToolCallResponse("call-ok", "good value"),
            CreateTextResponse("all done"));

        var client = new CompressionAwareLlmClient(chatClient, ReactHistoryCompressionMode.ObservationMasking);
        client.ModelInfo.HistoryCompression!.SummaryErrorLoop = true;
        client.ModelInfo.HistoryCompression.PreserveRecentRounds = 1;
        client.ModelInfo.HistoryCompression.ReactTokenThresholdPercent = 0.0001;

        var requestContext = new RequestContext
        {
            ChatMessages = [new ChatMessage(ChatRole.User, "do work")],
            FunctionCallEngine = new FailFirstToolCallEngine(),
            RequestOptions = new ChatOptions(),
            DialogId = "test-dialog",
        };

#pragma warning disable SKEXP0001
        var result = await ConsumeAllStepsAsync(client.SendRequestAsync(requestContext), CancellationToken.None);
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

    [Fact]
    public async Task LlmClientBase_ModeNoneWithSummaryErrorLoop_CompressesOlderErrorRound()
    {
        var chatClient = new RecordingSequentialChatClient(
            CreateToolCallResponse("call-err", "error value"),
            CreateToolCallResponse("call-ok", "good value"),
            CreateTextResponse("all done"));
        var client = new CompressionAwareLlmClient(chatClient, ReactHistoryCompressionMode.ObservationMasking);
        client.ModelInfo.HistoryCompression!.SummaryErrorLoop = true;
        client.ModelInfo.HistoryCompression.PreserveRecentRounds = 1;
        client.ModelInfo.HistoryCompression.ReactTokenThresholdPercent = 0.0001;

        var requestContext = new RequestContext
        {
            ChatMessages = [new ChatMessage(ChatRole.User, "do work")],
            FunctionCallEngine = new FailFirstToolCallEngine(),
            RequestOptions = new ChatOptions(),
            DialogId = "test-dialog",
        };

#pragma warning disable SKEXP0001
        var result = await ConsumeAllStepsAsync(client.SendRequestAsync(requestContext), CancellationToken.None);
#pragma warning restore SKEXP0001

        Assert.Null(result.Exception);
        Assert.Equal(3, chatClient.SeenHistories.Count);
        var historyForRound3 = chatClient.SeenHistories[2];
        Assert.DoesNotContain("call:noop:call-err", historyForRound3);
        Assert.Contains("error summary", historyForRound3);
    }

    #endregion

    #region Agent Tests

    [Fact]
    public async Task ReactAgentBase_Execute_SetsAgentIdOnRequestContext()
    {
        var chatClient = new SummaryOnlyLlmClient("done");
        var agent = new TestableReactAgent(chatClient, "MyTestAgent");

        var session = new MockDialogSession();
        await foreach (var step in agent.Execute(session, AgentRunOption.Default))
        {
            // Just consume
        }

        Assert.Equal("MyTestAgent", agent.LastRequestContext?.DialogId);
    }

    [Fact]
    public void ReactAgentBase_AgentId_DefaultsToName()
    {
        var chatClient = new SummaryOnlyLlmClient("done");
        var agent = new TestableReactAgent(chatClient, "DefaultNameAgent");

        Assert.Equal("DefaultNameAgent", agent.ExposedAgentId);
    }

    #endregion

    #region ResponseViewItemBase Rendering Tests

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

    #endregion

    #region SegmentedHistory Helper Methods

    private static SegmentedHistory CreateSegmentedHistoryWithTwoRounds()
    {
        var history = new SegmentedHistory
        {
            PreambleMessages =
            [
                new ChatMessage(ChatRole.System, "You are helpful."),
                new ChatMessage(ChatRole.User, "Fix the failing workflow."),
            ],
        };

        // Round 1
        var assistantRound1 = new ChatMessage(ChatRole.Assistant,
        [
            new TextReasoningContent("Need to inspect repository state."),
            new TextContent("I will inspect the repository."),
            new FunctionCallContent("call-1", "read_file", new Dictionary<string, object?>())
        ]);
        assistantRound1.TagLoopLevel(1, ReactHistoryMessageKind.Assistant);

        var observationRound1 = new ChatMessage(ChatRole.Tool,
        [
            new FunctionResultContent("call-1", "first observation")
        ]);
        observationRound1.TagLoopLevel(1, ReactHistoryMessageKind.Observation);

        history.Rounds.Add(new ReactRound
        {
            RoundNumber = 1,
            AssistantMessage = assistantRound1,
            ObservationMessage = observationRound1,
        });

        // Round 2
        var assistantRound2 = new ChatMessage(ChatRole.Assistant,
        [
            new TextReasoningContent("Need another file check."),
            new TextContent("I will inspect one more file."),
            new FunctionCallContent("call-2", "read_file", new Dictionary<string, object?>())
        ]);
        assistantRound2.TagLoopLevel(2, ReactHistoryMessageKind.Assistant);

        var observationRound2 = new ChatMessage(ChatRole.Tool,
        [
            new FunctionResultContent("call-2", "second observation")
        ]);
        observationRound2.TagLoopLevel(2, ReactHistoryMessageKind.Observation);

        history.Rounds.Add(new ReactRound
        {
            RoundNumber = 2,
            AssistantMessage = assistantRound2,
            ObservationMessage = observationRound2,
        });

        return history;
    }

    private static SegmentedHistory CreateSegmentedHistoryWithRecentErrorRound()
    {
        var history = new SegmentedHistory
        {
            PreambleMessages =
            [
                new ChatMessage(ChatRole.System, "You are helpful."),
                new ChatMessage(ChatRole.User, "Fix the failing workflow."),
            ],
        };

        // Round 1 (old, success)
        var assistantRound1 = new ChatMessage(ChatRole.Assistant,
        [
            new FunctionCallContent("call-ok-old", "old_tool", new Dictionary<string, object?>()),
        ]);
        assistantRound1.TagLoopLevel(1, ReactHistoryMessageKind.Assistant);

        var observationRound1 = new ChatMessage(ChatRole.Tool,
        [
            new FunctionResultContent("call-ok-old", "old success payload"),
        ]);
        observationRound1.TagLoopLevel(1, ReactHistoryMessageKind.Observation);

        history.Rounds.Add(new ReactRound
        {
            RoundNumber = 1,
            AssistantMessage = assistantRound1,
            ObservationMessage = observationRound1,
        });

        // Round 2 (recent, error)
        var assistantRound2 = new ChatMessage(ChatRole.Assistant,
        [
            new FunctionCallContent("call-err-recent", "recent_tool", new Dictionary<string, object?>()),
        ]);
        assistantRound2.TagLoopLevel(2, ReactHistoryMessageKind.Assistant);

        var observationRound2 = new ChatMessage(ChatRole.Tool,
        [
            new FunctionResultContent("call-err-recent", "recent error payload")
            {
                Exception = new InvalidOperationException("recent tool failure"),
            },
        ]);
        observationRound2.TagLoopLevel(2, ReactHistoryMessageKind.Observation);

        history.Rounds.Add(new ReactRound
        {
            RoundNumber = 2,
            AssistantMessage = assistantRound2,
            ObservationMessage = observationRound2,
        });

        return history;
    }

    #endregion

    #region Response/Event Helpers

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

    private static async Task<StepResult> ConsumeAllStepsAsync(
        IAsyncEnumerable<ReactStep> steps,
        CancellationToken cancellationToken)
    {
        StepResult? lastResult = null;
        await foreach (var step in steps.WithCancellation(cancellationToken))
        {
            // Consume all events in the step
            await foreach (var _ in step.WithCancellation(cancellationToken))
            {
            }

            lastResult = step.Result;
        }

        return lastResult!;
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

    #endregion

    #region Inner Test Classes

    private sealed class TestableReactAgent : ReactAgentBase
    {
        private readonly string _name;

        public TestableReactAgent(ILLMChatClient chatClient, string name)
            : base(chatClient, new AgentConfig(), MiniSweAgentConfigLoader.LoadDefaultWindowsConfig())
        {
            _name = name;
        }

        public override string Name => _name;
        public string ExposedAgentId => AgentId;
        public RequestContext? LastRequestContext { get; private set; }

        protected override Task<RequestContext?> BuildRequestContextAsync(ISession dialogSession,
            AgentRunOption option,
            CancellationToken cancellationToken)
        {
            var context = new RequestContext
            {
                ChatMessages = new List<ChatMessage> { new(ChatRole.User, "test") },
                FunctionCallEngine = new LoopingToolCallEngine(),
                RequestOptions = new ChatOptions(),
                DialogId = _name,
            };
            LastRequestContext = context;
            return Task.FromResult<RequestContext?>(context);
        }
    }

    private sealed class MockDialogSession : IDialogSession
    {
        public Guid ID { get; } = Guid.NewGuid();
        public IReadOnlyList<IDialogItem> VisualDialogItems { get; } = new List<IDialogItem>();

        public IResponseItem WorkingResponse => throw new NotSupportedException();
        
        public string? WorkingDirectory { get; } = null;

        public List<IChatHistoryItem> GetHistory() => [];

        public Task CutContextAsync(IRequestItem? requestItem = null) => Task.CompletedTask;

#pragma warning disable SKEXP0130
        public AIContextProvider[]? ContextProviders { get; } = null;
        public IPromptCommandAggregate? PromptCommand { get; set; }
#pragma warning restore SKEXP0130
        public string? SystemPrompt { get; } = null;
        public IEnumerable<Type> SupportedAgents { get; } = Array.Empty<Type>();
        public IFunctionGroupSource? ToolsSource { get; } = null;

        public Task<IResponse> NewResponse(RequestOption option, IRequestItem? insertBefore = null,
            CancellationToken token = default)
            => Task.FromResult<IResponse>(new RawResponseViewItem());
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

        protected override IChatClient GetChatClient(IRequestContext context)
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

    #endregion
}