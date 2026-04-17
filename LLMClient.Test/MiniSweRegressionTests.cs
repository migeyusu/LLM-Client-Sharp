using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Agent.Inspector;
using LLMClient.Agent.MiniSWE;
using LLMClient.Agent.Planner;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Configuration;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.ToolCall;
using LLMClient.ToolCall.DefaultPlugins;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Windows;
using FunctionCallContent = Microsoft.Extensions.AI.FunctionCallContent;
using FunctionResultContent = Microsoft.Extensions.AI.FunctionResultContent;
using TextContent = Microsoft.Extensions.AI.TextContent;

namespace LLMClient.Test;

[Collection("UI thread tests")]
public class MiniSweRegressionTests
{
    public MiniSweRegressionTests()
    {
        var services = new ServiceCollection()
            .AddTransient<AutoMapModelTypeConverter>()
            .AddSingleton<ITokensCounter, DefaultTokensCounter>()
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton<Profile, DialogItemPersistenceProfile>()
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
            .SetValue(agent, Array.Empty<KernelFunctionGroup>());

        var results = new List<StepResult>();
        await foreach (var step in agent.Execute(session, cancellationToken: CancellationToken.None))
        {
            // Consume all events so step.Result becomes available
            await foreach (var _ in step)
            {
            }

            if (step.Result != null)
                results.Add(step.Result);
        }

        Assert.Equal(2, results.Count);
        Assert.Equal(2, client.ChatHistoryCounts.Count);
        Assert.Equal(client.ChatHistoryCounts[0] + 1, client.ChatHistoryCounts[1]);
    }

    [Fact]
    public async Task ReactAgentBase_DuplicateAssistantText_BreaksLoop()
    {
        var client = new DuplicateTextChatClient();
        var request = new RequestViewItem("do something");
        var session = new TestTextDialogSession(request);
        var agent = new MiniSweAgent(client, new AgentOption
        {
            Platform = AgentPlatform.Windows,
            WorkingDirectory = Environment.CurrentDirectory,
        });
        typeof(MiniSweAgent)
            .GetField("_toolProviders", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(agent, Array.Empty<KernelFunctionGroup>());

        var results = new List<StepResult>();
        await foreach (var step in agent.Execute(session, cancellationToken: CancellationToken.None))
        {
            await foreach (var _ in step)
            {
            }

            if (step.Result != null)
                results.Add(step.Result);
        }

        Assert.Equal(2, results.Count);
        Assert.Equal(2, client.CallCount);
        Assert.All(results, r => Assert.Contains(r.Messages,
            m => m.Role == ChatRole.Assistant && m.Text == "duplicate output"));
    }

    [Fact]
    public void InspectAgent_UsesReadOnlyTools_AndCompletes()
    {
        TestFixture.RunInStaThread(() =>
        {
            var application = Application.Current ?? new Application();
            try
            {
                ExecuteAsync().GetAwaiter().GetResult();

                async Task ExecuteAsync()
                {
                    var client = new InspectRecordingChatClient();
                    var request = new RequestViewItem("inspect the project")
                    {
                        FunctionGroups =
                        [
                            new CheckableFunctionGroupTree(new FileSystemPlugin())
                        ]
                    };
                    var session = new TestTextDialogSession(request);
                    var agent = new InspectAgent(client, new AgentOption
                    {
                        Platform = AgentPlatform.Windows,
                        WorkingDirectory = Environment.CurrentDirectory,
                    });

                    var results = new List<StepResult>();
                    await foreach (var step in agent.Execute(session, CancellationToken.None))
                    {
                        await foreach (var _ in step)
                        {
                        }

                        if (step.Result != null)
                        {
                            results.Add(step.Result);
                        }
                    }

                    Assert.Single(results);
                    Assert.Contains("WinCLI", client.PluginNames);
                    Assert.DoesNotContain("FileSystem", client.PluginNames);
                }
            }
            finally
            {
                if (Application.Current == application)
                {
                    application.Shutdown();
                }
            }
        });
    }

    [Fact]
    public async Task InspectAgent_CompactsFinalSummary_AtCompletion()
    {
        var client = new CompactingInspectChatClient(returnInvalidCompactJson: false);
        var request = new RequestViewItem("inspect the agent flow");
        var session = new TestTextDialogSession(request);
        var agent = new InspectAgent(client, new AgentOption
        {
            Platform = AgentPlatform.Windows,
            WorkingDirectory = Environment.CurrentDirectory,
        });

        SetReadOnlyToolProviders(agent, Array.Empty<IAIFunctionGroup>());

        var result = new AgentTaskResult();
        var stepCount = 0;
        await foreach (var step in agent.Execute(session, CancellationToken.None))
        {
            stepCount++;
            await foreach (var _ in step)
            {
            }

            result.Add(step.Result);
        }

        Assert.Equal(2, stepCount);
        Assert.Contains("[0]|loop=1", client.LastCompactPrompt);
        Assert.Contains("[1]|loop=2", client.LastCompactPrompt);
        var content = result.GetContentAsString();
        Assert.DoesNotContain("Irrelevant tool chatter", content);
        Assert.Contains("Relevant inspection summary\nINSPECTION_COMPLETE", content);
        Assert.Contains("[INSPECT_COMPACT_HANDOFF]", content);
        Assert.Contains("Compacted inspection handoff\nINSPECTION_COMPLETE", content);
    }

    [Fact]
    public async Task InspectAgent_CompactFailure_FallsBackToRawMessages()
    {
        var client = new CompactingInspectChatClient(returnInvalidCompactJson: true);
        var request = new RequestViewItem("inspect the agent flow");
        var session = new TestTextDialogSession(request);
        var agent = new InspectAgent(client, new AgentOption
        {
            Platform = AgentPlatform.Windows,
            WorkingDirectory = Environment.CurrentDirectory,
        });

        SetReadOnlyToolProviders(agent, Array.Empty<IAIFunctionGroup>());

        var result = new AgentTaskResult();
        var stepCount = 0;
        await foreach (var step in agent.Execute(session, CancellationToken.None))
        {
            stepCount++;
            await foreach (var _ in step)
            {
            }

            result.Add(step.Result);
        }

        Assert.Equal(2, stepCount);
        Assert.Equal("Irrelevant tool chatter\nRelevant inspection summary\nINSPECTION_COMPLETE",
            result.GetContentAsString());
    }

    [Fact]
    public async Task PlannerAgent_CompactsFinalPlan_AtCompletion()
    {
        var client = new CompactingPlannerChatClient(returnInvalidCompactJson: false);
        var request = new RequestViewItem("plan the implementation");
        var session = new TestTextDialogSession(request);
        var agent = new PlannerAgent(client, new AgentOption
        {
            Platform = AgentPlatform.Windows,
            WorkingDirectory = Environment.CurrentDirectory,
        });

        SetReadOnlyToolProviders(agent, Array.Empty<IAIFunctionGroup>());

        var result = new AgentTaskResult();
        var stepCount = 0;
        await foreach (var step in agent.Execute(session, CancellationToken.None))
        {
            stepCount++;
            await foreach (var _ in step)
            {
            }

            result.Add(step.Result);
        }

        Assert.Equal(2, stepCount);
        Assert.Contains("[0]|loop=1", client.LastCompactPrompt);
        Assert.Contains("[1]|loop=2", client.LastCompactPrompt);
        var content = result.GetContentAsString();
        Assert.DoesNotContain("irrelevant planner chatter", content);
        Assert.Contains("Actionable plan draft\nPLANNING_COMPLETE", content);
        Assert.Contains("[PLANNER_COMPACT_HANDOFF]", content);
        Assert.Contains("Compacted execution plan\nPLANNING_COMPLETE", content);
    }

    [Fact]
    public async Task PlannerAgent_CompactFailure_FallsBackToRawMessages()
    {
        var client = new CompactingPlannerChatClient(returnInvalidCompactJson: true);
        var request = new RequestViewItem("plan the implementation");
        var session = new TestTextDialogSession(request);
        var agent = new PlannerAgent(client, new AgentOption
        {
            Platform = AgentPlatform.Windows,
            WorkingDirectory = Environment.CurrentDirectory,
        });

        SetReadOnlyToolProviders(agent, Array.Empty<IAIFunctionGroup>());

        var result = new AgentTaskResult();
        var stepCount = 0;
        await foreach (var step in agent.Execute(session, CancellationToken.None))
        {
            stepCount++;
            await foreach (var _ in step)
            {
            }

            result.Add(step.Result);
        }

        Assert.Equal(2, stepCount);
        Assert.Equal("irrelevant planner chatter\nActionable plan draft\nPLANNING_COMPLETE",
            result.GetContentAsString());
    }

    [Theory]
    [InlineData("INSPECTION_COMPLETE")]
    [InlineData("[INSPECT_COMPACT_HANDOFF]")]
    public async Task PlannerAgent_UsesInspectorHandoffFromHistory(string handoffMarker)
    {
        var client = new PlannerHistoryAwareChatClient();
        var request = new RequestViewItem("implement according to plan");
        var history = new IChatHistoryItem[]
        {
            new TestChatHistoryItem(new ChatMessage(ChatRole.Assistant,
                $"Inspector notes: focus FooService.BarMethod\n{handoffMarker}"))
        };
        var session = new TestTextDialogSession(request, history);
        var agent = new PlannerAgent(client, new AgentOption
        {
            Platform = AgentPlatform.Windows,
            WorkingDirectory = Environment.CurrentDirectory,
        });

        SetReadOnlyToolProviders(agent, Array.Empty<IAIFunctionGroup>());

        var result = new AgentTaskResult();
        var stepCount = 0;
        await foreach (var step in agent.Execute(session, CancellationToken.None))
        {
            stepCount++;
            await foreach (var _ in step)
            {
            }

            result.Add(step.Result);
        }

        Assert.Equal(1, stepCount);
        Assert.True(client.SawInspectorHandoff);
        Assert.Contains("FooService.BarMethod", client.FirstPlanningPrompt);
        Assert.Contains("Phased plan for FooService.BarMethod", result.GetContentAsString());
        Assert.Contains("PLANNING_COMPLETE", result.GetContentAsString());
    }

    [Fact]
    public async Task PlannerAgent_NoInspectorHandoffInHistory_DoesNotMarkFlag_AndStillPlans()
    {
        var client = new PlannerHistoryAwareChatClient();
        var request = new RequestViewItem("implement according to plan");
        var history = new IChatHistoryItem[]
        {
            new TestChatHistoryItem(new ChatMessage(ChatRole.Assistant,
                "Legacy context only: inspect artifacts not available yet"))
        };
        var session = new TestTextDialogSession(request, history);
        var agent = new PlannerAgent(client, new AgentOption
        {
            Platform = AgentPlatform.Windows,
            WorkingDirectory = Environment.CurrentDirectory,
        });

        SetReadOnlyToolProviders(agent, Array.Empty<IAIFunctionGroup>());

        var result = new AgentTaskResult();
        var stepCount = 0;
        await foreach (var step in agent.Execute(session, CancellationToken.None))
        {
            stepCount++;
            await foreach (var _ in step)
            {
            }

            result.Add(step.Result);
        }

        Assert.Equal(1, stepCount);
        Assert.False(client.SawInspectorHandoff);
        Assert.Contains("Legacy context only", client.FirstPlanningPrompt);
        Assert.Contains("Phased plan for FooService.BarMethod", result.GetContentAsString());
        Assert.Contains("PLANNING_COMPLETE", result.GetContentAsString());
    }

    [Fact]
    public async Task SendRequest_AgentFlowException_IsTreatedAsControlledCompletion()
    {
        var client = new AgentFlowLlmClient(new SingleToolCallChatClient());
        var engine = new AgentFlowCompletionEngine();
        var requestContext = new RequestContext
        {
            ChatMessages = [new ChatMessage(ChatRole.User, "finish the task")],
            FunctionCallEngine = engine,
            RequestOptions = new ChatOptions(),
        };

#pragma warning disable SKEXP0001
        var result = await client.SendRequestCompatAsync(requestContext, CancellationToken.None);
#pragma warning restore SKEXP0001

        Assert.Null(result.Exception);
        Assert.Equal(ChatFinishReason.Stop, result.FinishReason);
        Assert.Contains(result.Messages,
            message => message.Role == ChatRole.Assistant && message.Text == "final submission");
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
            ChatMessages = [new ChatMessage(ChatRole.User, "run the tool and finish")],
            FunctionCallEngine = engine,
            RequestOptions = new ChatOptions(),
        };

#pragma warning disable SKEXP0001
        var result = await client.SendRequestCompatAsync(requestContext, CancellationToken.None);
#pragma warning restore SKEXP0001

        Assert.Null(result.Exception);
        Assert.Equal(2, result.ValidCallTimes);
        AssertUsage(result.Usage, new UsageDetails
        {
            InputTokenCount = firstUsage.InputTokenCount + secondUsage.InputTokenCount,
            OutputTokenCount = firstUsage.OutputTokenCount + secondUsage.OutputTokenCount,
            TotalTokenCount = firstUsage.TotalTokenCount + secondUsage.TotalTokenCount,
        });
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
            ChatMessages = [new ChatMessage(ChatRole.User, "run the tool and fail later")],
            FunctionCallEngine = engine,
            RequestOptions = new ChatOptions(),
        };

#pragma warning disable SKEXP0001
        var result = await client.SendRequestCompatAsync(requestContext, CancellationToken.None);
#pragma warning restore SKEXP0001

        Assert.NotNull(result.Exception);
        Assert.Equal(1, result.ValidCallTimes);
        AssertUsage(result.Usage, firstUsage);
    }

    [Fact]
    public void ChatCallResult_Add()
    {
        var firstUsage = new UsageDetails { InputTokenCount = 7, OutputTokenCount = 2, TotalTokenCount = 9 };
        var secondUsage = new UsageDetails { InputTokenCount = 13, OutputTokenCount = 5, TotalTokenCount = 18 };
        var firstResult = new AgentTaskResult
        {
            Usage = firstUsage,
            ValidCallTimes = 1,
        };
        var secondResult = new AgentTaskResult
        {
            Usage = secondUsage,
            ValidCallTimes = 1,
        };

        firstResult.Add(secondResult);
        AssertUsage(firstResult.Usage, new UsageDetails
        {
            InputTokenCount = firstUsage.InputTokenCount + secondUsage.InputTokenCount,
            OutputTokenCount = firstUsage.OutputTokenCount + secondUsage.OutputTokenCount,
            TotalTokenCount = firstUsage.TotalTokenCount + secondUsage.TotalTokenCount,
        });
        
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WinCliPlugin_DangerousCommand_UsesExpectedPermissionSemantics(bool allow)
    {
        var plugin = new WinCLIPlugin(["danger"]);
        using var scope = CreatePermissionContext(allow);

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
        using var scope = CreatePermissionContext(allow);

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

        viewItem.Response.CancelCommand.Execute(null);
        var result = await processingTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(observedToken.IsCancellationRequested);
        Assert.False(result.IsInterrupt);
    }

    [Fact]
    public async Task LinearResponseViewItem_CancelCommand_AfterCompletion_DoesNotThrow()
    {
        var agent = new CompletedAgent();
        var parentSession = new TestDialogSessionViewModel();
        var session = new PassiveTextDialogSession();
        var viewItem = new LinearResponseViewItem(parentSession, agent);

        await viewItem.ProcessAsync(session, CancellationToken.None);

        var exception = Record.Exception(() => viewItem.Response.CancelCommand.Execute(null));
        Assert.Null(exception);
    }

    [Fact]
    public void LinearResponseViewItem_EliminateFailedHistoryCommand_RemovesFullyFailedRound()
    {
        TestFixture.RunInStaThread(() =>
        {
            var application = Application.Current ?? new Application();
            var parentSession = new TestDialogSessionViewModel();
            var agent = new DummyReactAgent();
            var viewItem = new LinearResponseViewItem(parentSession, agent);

            var systemMessage = new ChatMessage(ChatRole.System, "You are a helpful assistant.");

            var assistantRound1 = new ChatMessage(ChatRole.Assistant,
            [
                new TextContent("Let me check."),
                new FunctionCallContent("call-fail", "broken_tool", new Dictionary<string, object?>()),
            ]);
            ReactHistorySegmenter.TagMessage(assistantRound1, 1, ReactHistoryMessageKind.Assistant);

            var observationRound1 = new ChatMessage(ChatRole.Tool,
            [
                new FunctionResultContent("call-fail", "error message") { Exception = new InvalidOperationException("fail") },
            ]);
            ReactHistorySegmenter.TagMessage(observationRound1, 1, ReactHistoryMessageKind.Observation);

            var assistantRound2 = new ChatMessage(ChatRole.Assistant,
            [
                new TextContent("Let me try another."),
                new FunctionCallContent("call-ok", "working_tool", new Dictionary<string, object?>()),
            ]);
            ReactHistorySegmenter.TagMessage(assistantRound2, 2, ReactHistoryMessageKind.Assistant);

            var observationRound2 = new ChatMessage(ChatRole.Tool,
            [
                new FunctionResultContent("call-ok", "success result"),
            ]);
            ReactHistorySegmenter.TagMessage(observationRound2, 2, ReactHistoryMessageKind.Observation);

            viewItem.Response.Messages = new List<ChatMessage>
            {
                systemMessage, assistantRound1, observationRound1, assistantRound2, observationRound2
            };

            viewItem.EliminateFailedHistoryCommand.Execute(null);

            var remaining = viewItem.Response.Messages.ToList();
            Assert.Equal(3, remaining.Count);
            Assert.Contains(systemMessage, remaining);
            Assert.Contains(assistantRound2, remaining);
            Assert.Contains(observationRound2, remaining);
            Assert.DoesNotContain(assistantRound1, remaining);
            Assert.DoesNotContain(observationRound1, remaining);
        });
    }

    [Fact]
    public void LinearResponseViewItem_EliminateFailedHistoryCommand_KeepsMixedResultRound()
    {
        TestFixture.RunInStaThread(() =>
        {
            var application = Application.Current ?? new Application();
            var parentSession = new TestDialogSessionViewModel();
            var agent = new DummyReactAgent();
            var viewItem = new LinearResponseViewItem(parentSession, agent);

            var assistantRound1 = new ChatMessage(ChatRole.Assistant,
            [
                new TextContent("Let me check both."),
                new FunctionCallContent("call-ok", "working_tool", new Dictionary<string, object?>()),
                new FunctionCallContent("call-fail", "broken_tool", new Dictionary<string, object?>()),
            ]);
            ReactHistorySegmenter.TagMessage(assistantRound1, 1, ReactHistoryMessageKind.Assistant);

            var observationRound1 = new ChatMessage(ChatRole.Tool,
            [
                new FunctionResultContent("call-ok", "success result"),
                new FunctionResultContent("call-fail", "error message") { Exception = new InvalidOperationException("fail") },
            ]);
            ReactHistorySegmenter.TagMessage(observationRound1, 1, ReactHistoryMessageKind.Observation);

            viewItem.Response.Messages = new List<ChatMessage> { assistantRound1, observationRound1 };

            viewItem.EliminateFailedHistoryCommand.Execute(null);

            var remaining = viewItem.Response.Messages.ToList();
            Assert.Equal(2, remaining.Count);
            Assert.Contains(assistantRound1, remaining);
            Assert.Contains(observationRound1, remaining);
        });
    }

    [Fact]
    public void LinearResponseViewItem_EliminateFailedHistoryCommand_KeepsRoundWithoutToolCalls()
    {
        TestFixture.RunInStaThread(() =>
        {
            var application = Application.Current ?? new Application();
            var parentSession = new TestDialogSessionViewModel();
            var agent = new DummyReactAgent();
            var viewItem = new LinearResponseViewItem(parentSession, agent);

            var finalAnswer = new ChatMessage(ChatRole.Assistant,
            [
                new TextContent("The task is complete."),
            ]);
            ReactHistorySegmenter.TagMessage(finalAnswer, 1, ReactHistoryMessageKind.Assistant);

            viewItem.Response.Messages = new List<ChatMessage> { finalAnswer };

            viewItem.EliminateFailedHistoryCommand.Execute(null);

            var remaining = viewItem.Response.Messages.ToList();
            Assert.Single(remaining);
            Assert.Contains(finalAnswer, remaining);
        });
    }

    [Fact]
    public void LinearResponseViewItem_EliminateFailedHistoryCommand_KeepsSuccessRound()
    {
        TestFixture.RunInStaThread(() =>
        {
            var application = Application.Current ?? new Application();
            var parentSession = new TestDialogSessionViewModel();
            var agent = new DummyReactAgent();
            var viewItem = new LinearResponseViewItem(parentSession, agent);

            var assistantRound1 = new ChatMessage(ChatRole.Assistant,
            [
                new FunctionCallContent("call-ok", "working_tool", new Dictionary<string, object?>()),
            ]);
            ReactHistorySegmenter.TagMessage(assistantRound1, 1, ReactHistoryMessageKind.Assistant);

            var observationRound1 = new ChatMessage(ChatRole.Tool,
            [
                new FunctionResultContent("call-ok", "success result"),
            ]);
            ReactHistorySegmenter.TagMessage(observationRound1, 1, ReactHistoryMessageKind.Observation);

            viewItem.Response.Messages = new List<ChatMessage> { assistantRound1, observationRound1 };

            viewItem.EliminateFailedHistoryCommand.Execute(null);

            var remaining = viewItem.Response.Messages.ToList();
            Assert.Equal(2, remaining.Count);
            Assert.Contains(assistantRound1, remaining);
            Assert.Contains(observationRound1, remaining);
        });
    }

    private sealed class DummyReactAgent : ReactAgentBase
    {
        public DummyReactAgent() : base(new StubLlmClient(), new AgentOption(), new MiniSweAgentConfig())
        {
        }

        public override string Name => "DummyReactAgent";

        protected override Task<RequestContext?> BuildRequestContextAsync(ITextDialogSession dialogSession,
            CancellationToken cancellationToken)
            => Task.FromResult<RequestContext?>(null);
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

        public async IAsyncEnumerable<ReactStep> SendRequestAsync(IRequestContext context,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ChatHistoryCounts.Add(context.ReadonlyHistory.Count);
            _callCount++;
            var text = _callCount == 1 ? "retry required" : "COMPLETE_TASK_AND_SUBMIT_FINAL_OUTPUT";
            var message = new ChatMessage(ChatRole.Assistant, text);
            var step = new ReactStep();
            step.EmitText(text);
            var stepResult = new StepResult
            {
                FinishReason = ChatFinishReason.Stop,
                IsCompleted = true,
                Messages = [message],
            };
            if (_callCount == 1)
            {
                stepResult = new StepResult
                {
                    FinishReason = ChatFinishReason.Stop,
                    IsCompleted = true,
                    Messages = [message],
                    Exception = new Exception("retry"),
                };
            }

            step.Complete(stepResult);
            yield return step;
        }
    }

    private sealed class DuplicateTextChatClient : ILLMChatClient
    {
        private int _callCount;

        public int CallCount => _callCount;

        public string Name => "DuplicateTextChatClient";

        public ILLMAPIEndpoint Endpoint => EmptyLLMEndpoint.Instance;

        public IEndpointModel Model { get; } = new APIModelInfo
        {
            APIId = "fake-duplicate-model",
            Name = "Fake Duplicate Model",
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

        public async IAsyncEnumerable<ReactStep> SendRequestAsync(IRequestContext context,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _callCount++;
            const string text = "duplicate output";
            var message = new ChatMessage(ChatRole.Assistant, text);
            var step = new ReactStep();
            step.EmitText(text);
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

    private sealed class InspectRecordingChatClient : ILLMChatClient
    {
        public List<string> PluginNames { get; } = [];

        public string? LastCompactPrompt { get; private set; }

        public string Name => "InspectRecordingChatClient";

        public ILLMAPIEndpoint Endpoint => EmptyLLMEndpoint.Instance;

        public IEndpointModel Model { get; } = new APIModelInfo
        {
            APIId = "fake-inspect-model",
            Name = "Fake Inspect Model",
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

        public async IAsyncEnumerable<ReactStep> SendRequestAsync(IRequestContext context,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var lastPrompt = context.ReadonlyHistory.LastOrDefault()?.Text ?? string.Empty;
            if (lastPrompt.Contains("strict inspection compactor", StringComparison.Ordinal))
            {
                LastCompactPrompt = lastPrompt;
                var compactStep = new ReactStep();
                var compactJson = """{ "removeIndexes": [0], "summary": "Compact inspection summary\nINSPECTION_COMPLETE" }""";
                compactStep.EmitText(compactJson);
                compactStep.Complete(new StepResult
                {
                    FinishReason = ChatFinishReason.Stop,
                    IsCompleted = true,
                    Messages = [new ChatMessage(ChatRole.Assistant, compactJson)],
                });
                yield return compactStep;
                yield break;
            }

            PluginNames.Clear();
            PluginNames.AddRange(context.FunctionCallEngine.KernelPluginCollection.Select(plugin => plugin.Name));

            var text = "Inspection summary\nINSPECTION_COMPLETE";
            var message = new ChatMessage(ChatRole.Assistant, text);
            var step = new ReactStep();
            step.EmitText(text);
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

    private sealed class CompactingInspectChatClient : ILLMChatClient
    {
        private readonly bool _returnInvalidCompactJson;

        public CompactingInspectChatClient(bool returnInvalidCompactJson)
        {
            _returnInvalidCompactJson = returnInvalidCompactJson;
        }

        public string? LastCompactPrompt { get; private set; }

        public string Name => "CompactingInspectChatClient";

        public ILLMAPIEndpoint Endpoint => EmptyLLMEndpoint.Instance;

        public IEndpointModel Model { get; } = new APIModelInfo
        {
            APIId = "fake-compact-inspect-model",
            Name = "Fake Compact Inspect Model",
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

        public async IAsyncEnumerable<ReactStep> SendRequestAsync(IRequestContext context,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var lastPrompt = context.ReadonlyHistory.LastOrDefault()?.Text ?? string.Empty;
            if (lastPrompt.Contains("strict inspection compactor", StringComparison.Ordinal))
            {
                LastCompactPrompt = lastPrompt;
                var compactStep = new ReactStep();
                var compactText = _returnInvalidCompactJson
                    ? "not-json"
                    : """{ "removeIndexes": [0], "summary": "Compacted inspection handoff\nINSPECTION_COMPLETE" }""";
                compactStep.EmitText(compactText);
                compactStep.Complete(new StepResult
                {
                    FinishReason = ChatFinishReason.Stop,
                    IsCompleted = true,
                    Messages = [new ChatMessage(ChatRole.Assistant, compactText)],
                });
                yield return compactStep;
                yield break;
            }

            var firstStep = new ReactStep();
            firstStep.EmitDiagnostic(DiagLevel.Info, "searching workspace");
            firstStep.Complete(new StepResult
            {
                FinishReason = ChatFinishReason.Stop,
                IsCompleted = true,
                Messages = [new ChatMessage(ChatRole.Assistant, "Irrelevant tool chatter\n")],
            });
            yield return firstStep;

            var secondStep = new ReactStep();
            var finalText = "Relevant inspection summary\nINSPECTION_COMPLETE";
            secondStep.EmitText(finalText);
            secondStep.Complete(new StepResult
            {
                FinishReason = ChatFinishReason.Stop,
                IsCompleted = true,
                Messages = [new ChatMessage(ChatRole.Assistant, finalText)],
            });
            yield return secondStep;

            await Task.CompletedTask;
        }
    }

    private sealed class CompactingPlannerChatClient : ILLMChatClient
    {
        private readonly bool _returnInvalidCompactJson;

        public CompactingPlannerChatClient(bool returnInvalidCompactJson)
        {
            _returnInvalidCompactJson = returnInvalidCompactJson;
        }

        public string? LastCompactPrompt { get; private set; }

        public string Name => "CompactingPlannerChatClient";

        public ILLMAPIEndpoint Endpoint => EmptyLLMEndpoint.Instance;

        public IEndpointModel Model { get; } = new APIModelInfo
        {
            APIId = "fake-compact-planner-model",
            Name = "Fake Compact Planner Model",
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

        public async IAsyncEnumerable<ReactStep> SendRequestAsync(IRequestContext context,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var lastPrompt = context.ReadonlyHistory.LastOrDefault()?.Text ?? string.Empty;
            if (lastPrompt.Contains("strict planning compactor", StringComparison.Ordinal))
            {
                LastCompactPrompt = lastPrompt;
                var compactStep = new ReactStep();
                var compactText = _returnInvalidCompactJson
                    ? "not-json"
                    : """{ "removeIndexes": [0], "summary": "Compacted execution plan\nPLANNING_COMPLETE" }""";
                compactStep.EmitText(compactText);
                compactStep.Complete(new StepResult
                {
                    FinishReason = ChatFinishReason.Stop,
                    IsCompleted = true,
                    Messages = [new ChatMessage(ChatRole.Assistant, compactText)],
                });
                yield return compactStep;
                yield break;
            }

            var firstStep = new ReactStep();
            firstStep.EmitDiagnostic(DiagLevel.Info, "collecting project context");
            firstStep.Complete(new StepResult
            {
                FinishReason = ChatFinishReason.Stop,
                IsCompleted = true,
                Messages = [new ChatMessage(ChatRole.Assistant, "irrelevant planner chatter\n")],
            });
            yield return firstStep;

            var secondStep = new ReactStep();
            var finalText = "Actionable plan draft\nPLANNING_COMPLETE";
            secondStep.EmitText(finalText);
            secondStep.Complete(new StepResult
            {
                FinishReason = ChatFinishReason.Stop,
                IsCompleted = true,
                Messages = [new ChatMessage(ChatRole.Assistant, finalText)],
            });
            yield return secondStep;

            await Task.CompletedTask;
        }
    }

    private sealed class PlannerHistoryAwareChatClient : ILLMChatClient
    {
        public bool SawInspectorHandoff { get; private set; }

        public string? FirstPlanningPrompt { get; private set; }

        public string Name => "PlannerHistoryAwareChatClient";

        public ILLMAPIEndpoint Endpoint => EmptyLLMEndpoint.Instance;

        public IEndpointModel Model { get; } = new APIModelInfo
        {
            APIId = "fake-planner-history-model",
            Name = "Fake Planner History Model",
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

        public async IAsyncEnumerable<ReactStep> SendRequestAsync(IRequestContext context,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var lastPrompt = context.ReadonlyHistory.LastOrDefault()?.Text ?? string.Empty;
            if (lastPrompt.Contains("strict planning compactor", StringComparison.Ordinal))
            {
                var compactStep = new ReactStep();
                var compactText = """{ "removeIndexes": [0], "summary": "Compacted phased plan\nPLANNING_COMPLETE" }""";
                compactStep.EmitText(compactText);
                compactStep.Complete(new StepResult
                {
                    FinishReason = ChatFinishReason.Stop,
                    IsCompleted = true,
                    Messages = [new ChatMessage(ChatRole.Assistant, compactText)],
                });
                yield return compactStep;
                yield break;
            }

            FirstPlanningPrompt ??= string.Join("\n", context.ReadonlyHistory.Select(message => message.Text));
            SawInspectorHandoff = context.ReadonlyHistory.Any(message =>
                message.Role == ChatRole.Assistant
                && (message.Text?.Contains("INSPECTION_COMPLETE", StringComparison.Ordinal) == true
                    || message.Text?.Contains("[INSPECT_COMPACT_HANDOFF]", StringComparison.Ordinal) == true));

            var firstStep = new ReactStep();
            firstStep.EmitText("Phased plan for FooService.BarMethod\nPLANNING_COMPLETE");
            firstStep.Complete(new StepResult
            {
                FinishReason = ChatFinishReason.Stop,
                IsCompleted = true,
                Messages = [new ChatMessage(ChatRole.Assistant, "Phased plan for FooService.BarMethod\nPLANNING_COMPLETE")],
            });
            yield return firstStep;

            await Task.CompletedTask;
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

        public AgentFlowLlmClient(IChatClient chatClient, ITokensCounter? tokensCounter = null)
            : base(tokensCounter ?? new DefaultTokensCounter())
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

    private static IDisposable CreatePermissionContext(bool allow)
    {
        var step = new ReactStep();
        var chatContext = new ChatContext { CurrentStep = step, AutoApproveAllInvocations = allow };
        // Start a background task to consume permission requests from the step
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var evt in step)
                {
                    if (evt is PermissionRequest pr)
                    {
                        pr.Response.TrySetResult(allow);
                    }
                }
            }
            catch
            {
                // ignored
            }
        });
        return AsyncContextStore<ChatContext>.CreateInstance(chatContext);
    }

    private static void SetReadOnlyToolProviders(ReadOnlyCompactAgentBase agent,
        IReadOnlyList<IAIFunctionGroup> providers)
    {
        var field = typeof(ReadOnlyCompactAgentBase)
            .GetField("_toolProviders", BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(agent, providers);
    }

    private sealed class CancelAwareAgent : IAgent
    {
        public TaskCompletionSource<CancellationToken> TokenCaptured { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name => "CancelAwareAgent";

        public async IAsyncEnumerable<ReactStep> Execute(ITextDialogSession dialogSession,
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

    private sealed class CompletedAgent : IAgent
    {
        public string Name => "CompletedAgent";

        public async IAsyncEnumerable<ReactStep> Execute(ITextDialogSession dialogSession,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
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

        public TestTextDialogSession(IRequestItem request, IEnumerable<IChatHistoryItem>? history = null)
        {
            _history = history?.ToList() ?? [];
            _history.Add(request);
            DialogItems = [];
        }

        public IReadOnlyList<IDialogItem> DialogItems { get; }

        public List<IChatHistoryItem> GetHistory()
        {
            return _history;
        }

        public Task CutContextAsync(IRequestItem? requestItem = null)
        {
            return Task.CompletedTask;
        }

        public string? SystemPrompt => null;
    }

    private sealed class TestChatHistoryItem : IChatHistoryItem
    {
        public TestChatHistoryItem(params ChatMessage[] messages)
        {
            Messages = messages;
        }

        public IEnumerable<ChatMessage> Messages { get; }
    }

    private sealed class PassiveTextDialogSession : ITextDialogSession
    {
        public IReadOnlyList<IDialogItem> DialogItems { get; } = [];

        public List<IChatHistoryItem> GetHistory()
        {
            return [];
        }

        public Task CutContextAsync(IRequestItem? requestItem = null)
        {
            return Task.CompletedTask;
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