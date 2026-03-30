using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Agent.MiniSWE;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Configuration;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.ToolCall.DefaultPlugins;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using System.Runtime.CompilerServices;
using System.Reflection;
using FunctionCallContent = Microsoft.Extensions.AI.FunctionCallContent;
using FunctionResultContent = Microsoft.Extensions.AI.FunctionResultContent;
using TextContent = Microsoft.Extensions.AI.TextContent;

namespace LLMClient.Test;

public class MiniSweRegressionTests
{
    public MiniSweRegressionTests()
    {
        var services = new ServiceCollection()
            .AddTransient<AutoMapModelTypeConverter>()
            .AddSingleton<ITokensCounter, DefaultTokensCounter>()
            .AddMap()
            .BuildServiceProvider();
        BaseViewModel.ServiceLocator = services;
    }

    [Fact]
    public async Task MiniSweAgent_Retry_DoesNotDuplicateChatHistory()
    {
        var client = new RetryRecordingChatClient();
        var request = new RequestViewItem("fix the issue");
        var session = new TestTextDialogSession(request);
        var agent = new MiniSweAgent(client, new AgentOption
        {
            Platform = AgentPlatform.Windows,
            WorkingDirectory = Environment.CurrentDirectory,
        });
        typeof(MiniSweAgent)
            .GetField("_toolProviders", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(agent, Array.Empty<IAIFunctionGroup>());

        var results = new List<ChatCallResult>();
        await foreach (var result in agent.Execute(session, cancellationToken: CancellationToken.None))
        {
            results.Add(result);
        }

        Assert.Equal(2, results.Count);
        Assert.Equal(2, client.ChatHistoryCounts.Count);
        Assert.Equal(client.ChatHistoryCounts[0] + 1, client.ChatHistoryCounts[1]);
    }

    [Fact]
    public async Task SendRequest_AgentFlowException_IsTreatedAsControlledCompletion()
    {
        var client = new AgentFlowLlmClient(new SingleToolCallChatClient());
        var engine = new AgentFlowCompletionEngine();
        var requestContext = new RequestContext
        {
            ChatHistory = [new ChatMessage(ChatRole.User, "finish the task")],
            FunctionCallEngine = engine,
            RequestOptions = new ChatOptions(),
        };

#pragma warning disable SKEXP0001
        var result = await client.SendRequest(requestContext, cancellationToken: CancellationToken.None);
#pragma warning restore SKEXP0001

        Assert.Null(result.Exception);
        Assert.Equal(ChatFinishReason.Stop, result.FinishReason);
        Assert.Contains(result.Messages, message => message.Role == ChatRole.Assistant && message.Text == "final submission");
    }

    [Fact]
    public async Task SendRequest_MultiTurnFunctionCall_TracksLastSuccessfulUsageSeparatelyFromTotalUsage()
    {
        var firstUsage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 2, TotalTokenCount = 12 };
        var secondUsage = new UsageDetails { InputTokenCount = 30, OutputTokenCount = 4, TotalTokenCount = 34 };
        var client = new AgentFlowLlmClient(new SequentialChatClient(
            CreateToolCallResponse("call-1", firstUsage),
            CreateTextResponse("all done", secondUsage)));
        var engine = new LoopingToolCallEngine();
        var requestContext = new RequestContext
        {
            ChatHistory = [new ChatMessage(ChatRole.User, "run the tool and finish")],
            FunctionCallEngine = engine,
            RequestOptions = new ChatOptions(),
        };

#pragma warning disable SKEXP0001
        var result = await client.SendRequest(requestContext, cancellationToken: CancellationToken.None);
#pragma warning restore SKEXP0001

        Assert.Null(result.Exception);
        Assert.Equal(2, result.ValidCallTimes);
        AssertUsage(result.Usage, new UsageDetails
        {
            InputTokenCount = firstUsage.InputTokenCount + secondUsage.InputTokenCount,
            OutputTokenCount = firstUsage.OutputTokenCount + secondUsage.OutputTokenCount,
            TotalTokenCount = firstUsage.TotalTokenCount + secondUsage.TotalTokenCount,
        });
        AssertUsage(result.LastSuccessfulUsage, secondUsage);
    }

    [Fact]
    public async Task SendRequest_LaterRoundFailure_PreservesPreviousLastSuccessfulUsage()
    {
        var firstUsage = new UsageDetails { InputTokenCount = 11, OutputTokenCount = 3, TotalTokenCount = 14 };
        var client = new AgentFlowLlmClient(new SequentialChatClient(
            CreateToolCallResponse("call-1", firstUsage),
            new InvalidOperationException("boom")));
        var engine = new LoopingToolCallEngine();
        var requestContext = new RequestContext
        {
            ChatHistory = [new ChatMessage(ChatRole.User, "run the tool and fail later")],
            FunctionCallEngine = engine,
            RequestOptions = new ChatOptions(),
        };

#pragma warning disable SKEXP0001
        var result = await client.SendRequest(requestContext, cancellationToken: CancellationToken.None);
#pragma warning restore SKEXP0001

        Assert.NotNull(result.Exception);
        Assert.Equal(1, result.ValidCallTimes);
        AssertUsage(result.Usage, firstUsage);
        AssertUsage(result.LastSuccessfulUsage, firstUsage);
    }

    [Fact]
    public void ChatCallResult_Add_UsesLatestSuccessfulUsageAcrossAggregation()
    {
        var firstUsage = new UsageDetails { InputTokenCount = 7, OutputTokenCount = 2, TotalTokenCount = 9 };
        var secondUsage = new UsageDetails { InputTokenCount = 13, OutputTokenCount = 5, TotalTokenCount = 18 };
        var firstResult = new ChatCallResult
        {
            Usage = firstUsage,
            LastSuccessfulUsage = firstUsage,
            ValidCallTimes = 1,
        };
        var secondResult = new ChatCallResult
        {
            Usage = secondUsage,
            LastSuccessfulUsage = secondUsage,
            ValidCallTimes = 1,
        };
        var interruptedResult = new ChatCallResult
        {
            Exception = new Exception("interrupted"),
            ValidCallTimes = 0,
        };

        var combined = firstResult + secondResult;
        AssertUsage(combined.Usage, new UsageDetails
        {
            InputTokenCount = firstUsage.InputTokenCount + secondUsage.InputTokenCount,
            OutputTokenCount = firstUsage.OutputTokenCount + secondUsage.OutputTokenCount,
            TotalTokenCount = firstUsage.TotalTokenCount + secondUsage.TotalTokenCount,
        });
        AssertUsage(combined.LastSuccessfulUsage, secondUsage);

        var preserved = combined + interruptedResult;
        AssertUsage(preserved.LastSuccessfulUsage, secondUsage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WinCliPlugin_DangerousCommand_UsesExpectedPermissionSemantics(bool allow)
    {
        var plugin = new WinCLIPlugin(["danger"]);
        using var scope = AsyncContextStore<ChatContext>.CreateInstance(new ChatContext(new PermissionInteractor(allow)));

        if (!allow)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                plugin.ExecuteCommandAsync(new Kernel(), "danger argument"));
            return;
        }

        var result = await plugin.ExecuteCommandAsync(new Kernel(), "danger argument");
        Assert.NotNull(result);
        Assert.NotEqual("用户拒绝执行命令: danger argument", result.ExceptionInfo);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WslCliPlugin_DangerousCommand_UsesExpectedPermissionSemantics(bool allow)
    {
        var plugin = new WslCLIPlugin(["danger"]);
        using var scope = AsyncContextStore<ChatContext>.CreateInstance(new ChatContext(new PermissionInteractor(allow)));

        if (!allow)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                plugin.ExecuteCommandAsync(new Kernel(), "danger argument"));
            return;
        }

        var result = await plugin.ExecuteCommandAsync(new Kernel(), "danger argument");
        Assert.NotNull(result);
        Assert.NotEqual("用户拒绝执行命令: danger argument", result.ExceptionInfo);
    }

    [Fact]
    public async Task LinearResponseViewItem_CancelCommand_CancelsAgentToken()
    {
        var agent = new CancelAwareAgent();
        var parentSession = new TestDialogSessionViewModel();
        var session = new PassiveTextDialogSession();
        var viewItem = new LinearResponseViewItem(parentSession, agent);

        var processingTask = viewItem.ProcessAsync(session, CancellationToken.None);
        var observedToken = await agent.TokenCaptured.Task.WaitAsync(TimeSpan.FromSeconds(5));

        viewItem.CancelCommand.Execute(null);
        var result = await processingTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(observedToken.IsCancellationRequested);
        Assert.False(result.IsInterrupt);
    }

    private sealed class RetryRecordingChatClient : ILLMChatClient
    {
        private int _callCount;

        public List<int> ChatHistoryCounts { get; } = [];

        public string Name => "RetryRecordingChatClient";

        public ILLMAPIEndpoint Endpoint => EmptyLLMEndpoint.Instance;

        public IEndpointModel Model { get; } = new APIModelInfo
        {
            APIId = "fake-mini-swe-model",
            Name = "Fake MiniSWE Model",
            Endpoint = EmptyLLMEndpoint.Instance,
            SupportFunctionCall = true,
            SupportStreaming = false,
            SupportSystemPrompt = true,
            FunctionCallOnStreaming = true,
            SupportTextGeneration = true,
            TopPEnable = true,
            TopKEnable = true,
            TemperatureEnable = true,
            MaxTokensEnable = true,
            FrequencyPenaltyEnable = true,
            PresencePenaltyEnable = true,
            SeedEnable = true,
            PriceCalculator = new TokenBasedPriceCalculator()
        };

        public IModelParams Parameters { get; set; } = new DefaultModelParam { Streaming = false };

        public bool IsResponding { get; set; }

        public Task<ChatCallResult> SendRequest(RequestContext context, IInvokeInteractor? interactor = null,
            CancellationToken cancellationToken = default)
        {
            ChatHistoryCounts.Add(context.ChatHistory.Count);
            _callCount++;
            var text = _callCount == 1 ? "retry required" : "COMPLETE_TASK_AND_SUBMIT_FINAL_OUTPUT";
            var message = new ChatMessage(ChatRole.Assistant, text);
            context.ChatHistory.Add(message);

            var result = new ChatCallResult
            {
                Messages = [message],
                FinishReason = ChatFinishReason.Stop,
            };
            if (_callCount == 1)
            {
                result.Exception = new Exception("retry");
            }

            return Task.FromResult(result);
        }
    }

    private sealed class AgentFlowCompletionEngine : FunctionCallEngine
    {
        public AgentFlowCompletionEngine()
        {
            KernelPluginCollection.AddFromFunctions("Test",
            [
                KernelFunctionFactory.CreateFromMethod(
                    () => "ok",
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
            throw new SubmittedException("final submission", new ChatMessage(ChatRole.Assistant, "final submission"));
        }
    }

    private sealed class AgentFlowLlmClient : LlmClientBase
    {
        private readonly IChatClient _chatClient;
        private readonly IEndpointModel _model;

        public AgentFlowLlmClient(IChatClient chatClient)
        {
            _chatClient = chatClient;
            _model = new APIModelInfo
            {
                APIId = "fake-agent-flow-model",
                Name = "Fake Agent Flow Model",
                Endpoint = EmptyLLMEndpoint.Instance,
                SupportFunctionCall = true,
                SupportStreaming = false,
                SupportSystemPrompt = true,
                FunctionCallOnStreaming = true,
                SupportTextGeneration = true,
                TopPEnable = true,
                TopKEnable = true,
                TemperatureEnable = true,
                MaxTokensEnable = true,
                FrequencyPenaltyEnable = true,
                PresencePenaltyEnable = true,
                SeedEnable = true,
                PriceCalculator = new TokenBasedPriceCalculator()
            };
            Parameters = new DefaultModelParam { Streaming = false };
        }

        public override string Name => "AgentFlowLlmClient";

        public override ILLMAPIEndpoint Endpoint => EmptyLLMEndpoint.Instance;

        public override IEndpointModel Model => _model;

        protected override IChatClient GetChatClient()
        {
            return _chatClient;
        }
    }

    private sealed class SingleToolCallChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options,
            CancellationToken cancellationToken = default)
        {
            var message = new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("call-1", "noop", new Dictionary<string, object?>())]);
            var response = new ChatResponse([message])
            {
                FinishReason = ChatFinishReason.ToolCalls,
                Usage = new UsageDetails(),
            };
            return Task.FromResult(response);
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
    }

    private sealed class SequentialChatClient(params object[] responses) : IChatClient
    {
        private readonly Queue<object> _responses = new(responses);

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options,
            CancellationToken cancellationToken = default)
        {
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No more responses configured.");
            }

            var response = _responses.Dequeue();
            if (response is Exception exception)
            {
                throw exception;
            }

            return Task.FromResult((ChatResponse)response);
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
    }

    private sealed class LoopingToolCallEngine : FunctionCallEngine
    {
        public LoopingToolCallEngine()
        {
            KernelPluginCollection.AddFromFunctions("Test",
            [
                KernelFunctionFactory.CreateFromMethod(
                    () => "ok",
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

    private sealed class PermissionInteractor(bool allow) : IInvokeInteractor
    {
        public void Info(string message)
        {
        }

        public void Error(string message)
        {
        }

        public void Warning(string message)
        {
        }

        public void Write(string message)
        {
        }

        public void WriteLine(string? message = null)
        {
        }

        public Task<bool> WaitForPermission(string title, string message)
        {
            return Task.FromResult(allow);
        }

        public Task<bool> WaitForPermission(object content)
        {
            return Task.FromResult(allow);
        }
    }

    private sealed class CancelAwareAgent : IAgent
    {
        public TaskCompletionSource<CancellationToken> TokenCaptured { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name => "CancelAwareAgent";

        public async IAsyncEnumerable<ChatCallResult> Execute(ITextDialogSession dialogSession,
            IInvokeInteractor? interactor = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            TokenCaptured.TrySetResult(cancellationToken);
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(10, CancellationToken.None);
            }

            yield break;
        }
    }

    private static ChatResponse CreateToolCallResponse(string callId, UsageDetails usage)
    {
        var message = new ChatMessage(ChatRole.Assistant,
        [
            new FunctionCallContent(callId, "noop", new Dictionary<string, object?>()),
            new UsageContent(new UsageDetails
            {
                InputTokenCount = usage.InputTokenCount,
                OutputTokenCount = usage.OutputTokenCount,
                TotalTokenCount = usage.TotalTokenCount,
            })
        ]);

        return new ChatResponse([message])
        {
            FinishReason = ChatFinishReason.ToolCalls,
        };
    }

    private static ChatResponse CreateTextResponse(string text, UsageDetails usage)
    {
        var message = new ChatMessage(ChatRole.Assistant,
        [
            new TextContent(text),
            new UsageContent(new UsageDetails
            {
                InputTokenCount = usage.InputTokenCount,
                OutputTokenCount = usage.OutputTokenCount,
                TotalTokenCount = usage.TotalTokenCount,
            })
        ]);

        return new ChatResponse([message])
        {
            FinishReason = ChatFinishReason.Stop,
        };
    }

    private static void AssertUsage(UsageDetails? actual, UsageDetails expected)
    {
        var usage = Assert.IsType<UsageDetails>(actual);
        Assert.Equal(expected.InputTokenCount, usage.InputTokenCount);
        Assert.Equal(expected.OutputTokenCount, usage.OutputTokenCount);
        Assert.Equal(expected.TotalTokenCount, usage.TotalTokenCount);
    }

    private sealed class TestTextDialogSession : ITextDialogSession
    {
        private readonly List<IChatHistoryItem> _history;

        public TestTextDialogSession(IRequestItem request)
        {
            _history = [request];
            DialogItems = [];
        }

        public IReadOnlyList<IDialogItem> DialogItems { get; }

        public List<IChatHistoryItem> GetHistory()
        {
            return _history;
        }

        public string? SystemPrompt => null;
    }

    private sealed class PassiveTextDialogSession : ITextDialogSession
    {
        public IReadOnlyList<IDialogItem> DialogItems { get; } = [];

        public List<IChatHistoryItem> GetHistory()
        {
            return [];
        }

        public string? SystemPrompt => null;
    }

    private sealed class TestDialogSessionViewModel : DialogSessionViewModel
    {
        public TestDialogSessionViewModel() : base(new GlobalOptions(), new Summarizer(new GlobalOptions()), null)
        {
        }

        public override string? SystemPrompt => null;
    }
}

