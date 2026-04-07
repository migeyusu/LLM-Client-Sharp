using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using LLMClient.Abstraction;
using LLMClient.Component;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
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
        var requestContext = new RequestContext
        {
            ChatHistory = [new ChatMessage(ChatRole.User, "solve the issue")],
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
    public async Task PreambleSummary_SkipsWhenBelowThreshold()
    {
        var chatHistory = CreateHistoryWithPreambleAndRounds(shortPreamble: true);
        var summaryClient = new SummaryOnlyLlmClient("should not be called");
        var summarizer = new Summarizer(new GlobalOptions());
        var strategy = new PreambleSummaryChatHistoryCompressionStrategy(summarizer);

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
        var summaryClient = new SummaryOnlyLlmClient("investigated auth module, fixed token refresh bug in AuthService.cs");
        var summarizer = new Summarizer(new GlobalOptions());
        var strategy = new PreambleSummaryChatHistoryCompressionStrategy(summarizer);

        await strategy.CompressAsync(new ChatHistoryCompressionContext
        {
            ChatHistory = chatHistory,
            Options = new ReactHistoryCompressionOptions
            {
                PreambleCompression = true,
                PreambleTokenThresholdPercent = 10, // very low threshold to trigger compression
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
        var strategy = new PreambleSummaryChatHistoryCompressionStrategy(summarizer);

        await strategy.CompressAsync(new ChatHistoryCompressionContext
        {
            ChatHistory = chatHistory,
            Options = new ReactHistoryCompressionOptions
            {
                PreambleCompression = true,
                PreambleTokenThresholdPercent = 10,
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
        var strategy = new PreambleSummaryChatHistoryCompressionStrategy(summarizer);

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
        client.ModelInfo.HistoryCompression.PreambleCompression = true;
        client.ModelInfo.HistoryCompression.PreambleTokenThresholdPercent = 10;

        // Build history: system + previous task messages + current user message
        var chatHistory = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            new(ChatRole.User, "Previous task: fix authentication"),
            new(ChatRole.Assistant, "I analyzed the auth module and found a token refresh bug in AuthService.cs. I applied the fix and verified it works."),
            new(ChatRole.User, "Now fix the CI pipeline."),
        };

        var requestContext = new RequestContext
        {
            ChatHistory = chatHistory,
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
        client.ModelInfo.HistoryCompression.PreambleTokenThresholdPercent = 10;

        var requestContext = new RequestContext
        {
            ChatHistory =
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
    public async Task LlmClientBase_ObservationMasking_EmitsCompressionLoopEvents()
    {
        var chatClient = new RecordingSequentialChatClient(
            CreateToolCallResponse("call-1", "first observation"),
            CreateToolCallResponse("call-2", "second observation"),
            CreateTextResponse("done"));
        var client = new CompressionAwareLlmClient(chatClient, ReactHistoryCompressionMode.ObservationMasking);
        client.ModelInfo.HistoryCompression.PreserveRecentRounds = 1;

        var requestContext = new RequestContext
        {
            ChatHistory = [new ChatMessage(ChatRole.User, "solve the issue")],
            FunctionCallEngine = new LoopingToolCallEngine(),
            RequestOptions = new ChatOptions(),
        };

#pragma warning disable SKEXP0001
        var stepEvents = await CollectStepEventsAsync(client.SendRequestAsync(requestContext), CancellationToken.None);
#pragma warning restore SKEXP0001

        Assert.Equal(3, stepEvents.Count);
        Assert.Contains(stepEvents[0],
            evt => evt is HistoryCompressionStarted { Kind: HistoryCompressionKind.ObservationMasking });
        Assert.Contains(stepEvents[0],
            evt => evt is HistoryCompressionCompleted
            {
                Kind: HistoryCompressionKind.ObservationMasking,
                Applied: false,
            });
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
        var loops = new ObservableCollection<ReactLoopViewModel>();

        await ResponseViewItemBase.ConsumeReactStepsAsync(CreateCompressionEventSteps(), loops, _ => { },
            CancellationToken.None);

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
            // Previous task messages (no round tags — simulating persisted history from prior task)
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
        ReactHistorySegmenter.TagMessage(assistantRound1, 1, ReactHistoryMessageKind.Assistant);
        history.Add(assistantRound1);

        var observationRound1 = new ChatMessage(ChatRole.Tool,
        [
            new FunctionResultContent("call-current-1", "pipeline config file content")
        ]);
        ReactHistorySegmenter.TagMessage(observationRound1, 1, ReactHistoryMessageKind.Observation);
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
        ReactHistorySegmenter.TagMessage(assistantRound1, 1, ReactHistoryMessageKind.Assistant);
        history.Add(assistantRound1);

        var observationRound1 = new ChatMessage(ChatRole.Tool,
        [
            new FunctionResultContent("call-1", "first observation")
        ]);
        ReactHistorySegmenter.TagMessage(observationRound1, 1, ReactHistoryMessageKind.Observation);
        history.Add(observationRound1);

        var assistantRound2 = new ChatMessage(ChatRole.Assistant,
        [
            new TextReasoningContent("Need another file check."),
            new TextContent("I will inspect one more file."),
            new FunctionCallContent("call-2", "read_file", new Dictionary<string, object?>())
        ]);
        ReactHistorySegmenter.TagMessage(assistantRound2, 2, ReactHistoryMessageKind.Assistant);
        history.Add(assistantRound2);

        var observationRound2 = new ChatMessage(ChatRole.Tool,
        [
            new FunctionResultContent("call-2", "second observation")
        ]);
        ReactHistorySegmenter.TagMessage(observationRound2, 2, ReactHistoryMessageKind.Observation);
        history.Add(observationRound2);

        return history;
    }

    private static ChatResponse CreateToolCallResponse(string callId, string observation)
    {
        var message = new ChatMessage(ChatRole.Assistant,
        [
            new FunctionCallContent(callId, "noop", new Dictionary<string, object?>
            {
                ["value"] = observation,
            })
        ]);

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

        public CompressionAwareLlmClient(IChatClient chatClient, ReactHistoryCompressionMode mode)
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

        public async IAsyncEnumerable<ReactStep> SendRequestAsync(RequestContext requestContext,
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
                            parts.Add($"{message.Role}:result:{functionResultContent.CallId}:{functionResultContent.Result}");
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
}

