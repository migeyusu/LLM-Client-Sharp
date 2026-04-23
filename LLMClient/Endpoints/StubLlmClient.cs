using System.Runtime.CompilerServices;
using LLMClient.Abstraction;
using LLMClient.Component.Render;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints.OpenAIAPI;
using Microsoft.Extensions.AI;

namespace LLMClient.Endpoints;

public class StubLlmClient : ILLMChatClient
{
    public IEndpointModel Model { get; } = StubLLMChatModel.Instance;
    public IModelParams Parameters { get; set; } = new DefaultModelParam();
    public string Name { get; } = "StubLlmClient";
    public bool IsResponding { get; set; }

    /// <summary>
    /// 模拟多轮 ReAct 的 Stub 数据定义
    /// </summary>
    private static readonly ReactLoop[] SimulatedLoops =
    [
        // ── Loop 1: 思考 → 调用 search ──
        new()
        {
            ThinkingContent = "用户在问关于WPF中数据绑定的问题，我需要先搜索相关文档来获取最新信息。",
            TextContent = "让我先搜索一下相关的文档信息。",
            FunctionCall = new SimulatedFunctionCall
            {
                CallId = "call_001",
                Name = "web_search",
                Arguments = """{"query": "WPF data binding best practices 2026"}"""
            },
            FunctionResult = new SimulatedFunctionResult
            {
                CallId = "call_001",
                Result = """Found 3 results:\n1. Microsoft Docs - Data Binding Overview\n2. WPF Tutorial - MVVM Pattern\n3. Stack Overflow - Common binding mistakes"""
            }
            ,
            Usage = new UsageDetails
            {
                InputTokenCount = 64,
                OutputTokenCount = 32,
                TotalTokenCount = 96,
            }
        },
        // ── Loop 2: 再次思考 → 调用 read_file ──
        new()
        {
            ThinkingContent = "搜索结果给了一些方向，但我需要读取项目中的具体代码来给出更有针对性的建议。让我查看一下用户的 ViewModel 实现。",
            TextContent = "找到了一些参考资料，现在让我查看项目中的代码实现。",
            FunctionCall = new SimulatedFunctionCall
            {
                CallId = "call_002",
                Name = "read_file",
                Arguments = """{"path": "src/ViewModels/MainViewModel.cs", "offset": 1, "limit": 50}"""
            },
            FunctionResult = new SimulatedFunctionResult
            {
                CallId = "call_002",
                Result = """public class MainViewModel : INotifyPropertyChanged\n{\n    private string _title;\n    public string Title\n    {\n        get => _title;\n        set { _title = value; OnPropertyChanged(); }\n    }\n}"""
            }
            ,
            Usage = new UsageDetails
            {
                InputTokenCount = 48,
                OutputTokenCount = 24,
                TotalTokenCount = 72,
            }
        },
        // ── Loop 3: 最终回复，无工具调用 ──
        new()
        {
            ThinkingContent = "现在我有了足够的信息来给出一个完整的回答。用户的ViewModel实现基本正确，但可以改进。",
            TextContent =
                """
                ## WPF 数据绑定最佳实践

                根据搜索结果和您项目的代码，以下是一些建议：

                ### 1. 使用 `ObservableObject` 基类
                您当前手动实现了 `INotifyPropertyChanged`，建议使用 CommunityToolkit.Mvvm 提供的 `ObservableObject`：

                ```csharp
                public partial class MainViewModel : ObservableObject
                {
                    [ObservableProperty]
                    private string _title;
                }
                ```

                ### 2. 集合绑定使用 `ObservableCollection<T>`
                确保列表类型的属性使用 `ObservableCollection<T>` 而不是 `List<T>`，这样 UI 才能自动响应集合变化。

                ### 3. 避免常见错误
                - ❌ 不要在构造函数中多次赋值触发通知
                - ✅ 使用 `SetProperty` 方法来简化属性通知
                - ✅ 在 XAML 中使用 `x:Bind` 获得编译期检查（UWP/WinUI）

                > **总结**：您的基础实现是正确的，主要可以通过引入源生成器来减少样板代码。
                """,
            FunctionCall = null,
            FunctionResult = null
            ,
            Usage = new UsageDetails
            {
                InputTokenCount = 256,
                OutputTokenCount = 512,
                TotalTokenCount = 768,
            }
        }
    ];

    public async IAsyncEnumerable<ReactStep> SendRequestAsync(IRequestContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IsResponding = true;
        try
        {
            var random = new Random();
            var streaming = Parameters.Streaming;

            for (int loopIndex = 0; loopIndex < SimulatedLoops.Length; loopIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var loop = SimulatedLoops[loopIndex];
                var step = new ReactStep();

                var producerTask = Task.Run(async () =>
                {
                    try
                    {
                        // 1. Reasoning / Thinking content
                        if (!string.IsNullOrEmpty(loop.ThinkingContent))
                        {
                            if (streaming)
                            {
                                await StreamToStep(step, loop.ThinkingContent, random, isReasoning: true,
                                    cancellationToken);
                            }
                            else
                            {
                                step.EmitReasoning(loop.ThinkingContent);
                            }
                        }

                        // 2. Text content
                        if (!string.IsNullOrEmpty(loop.TextContent))
                        {
                            if (streaming)
                            {
                                await StreamToStep(step, loop.TextContent, random, isReasoning: false,
                                    cancellationToken);
                            }
                            else
                            {
                                step.EmitText(loop.TextContent);
                            }
                        }

                        // 3. Function call
                        if (loop.FunctionCall != null)
                        {
                            var fc = loop.FunctionCall;
                            var functionCallContent = new FunctionCallContent(fc.CallId, fc.Name,
                                new Dictionary<string, object?> { ["raw"] = fc.Arguments });
                            step.Emit(new FunctionCallStarted(functionCallContent));

                            await Task.Delay(random.Next(500, 1500), cancellationToken);

                            if (loop.FunctionResult != null)
                            {
                                step.Emit(new FunctionCallCompleted(
                                    loop.FunctionResult.CallId, fc.Name, loop.FunctionResult.Result, null));
                            }

                            step.Complete(new StepResult
                            {
                                IsCompleted = false,
                                Latency = random.Next(50, 200),
                                Usage = loop.Usage ?? new UsageDetails
                                {
                                    // Default simulated token usage for an intermediate function-call step
                                    InputTokenCount = 128,
                                    OutputTokenCount = 64,
                                    TotalTokenCount = 192,
                                },
                                Messages =
                                [
                                    new ChatMessage(ChatRole.Assistant, loop.TextContent ?? "")
                                ],
                            });
                        }
                        else
                        {
                            step.Complete(new StepResult
                            {
                                IsCompleted = true,
                                FinishReason = ChatFinishReason.Stop,
                                Latency = random.Next(50, 200),
                                Usage = loop.Usage ?? new UsageDetails
                                {
                                    InputTokenCount = 256,
                                    OutputTokenCount = 512,
                                    TotalTokenCount = 768,
                                },
                                Messages =
                                [
                                    new ChatMessage(ChatRole.Assistant, loop.TextContent ?? "")
                                ],
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        step.CompleteWithError(ex);
                    }
                }, cancellationToken);

                yield return step;
                await producerTask;

                if (step.Result is { IsCompleted: true } or { Exception: not null })
                    break;
            }
        }
        finally
        {
            IsResponding = false;
        }
    }

    private static async Task StreamToStep(ReactStep step, string text, Random random,
        bool isReasoning, CancellationToken cancellationToken)
    {
        int currentIndex = 0;
        while (currentIndex < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int len = Math.Min(random.Next(3, 9), text.Length - currentIndex);
            var chunk = text.Substring(currentIndex, len);
            if (isReasoning)
                step.EmitReasoning(chunk);
            else
                step.EmitText(chunk);
            currentIndex += len;
            await Task.Delay(random.Next(30, 80), cancellationToken);
        }
    }

    public ILLMAPIEndpoint Endpoint { get; } = new EmptyLLMEndpoint();

    // ── 内部模拟数据结构 ──

    private class ReactLoop
    {
        public string? ThinkingContent { get; init; }
        public string? TextContent { get; init; }
        public SimulatedFunctionCall? FunctionCall { get; init; }
        public SimulatedFunctionResult? FunctionResult { get; init; }
        public UsageDetails? Usage { get; init; }
    }

    private class SimulatedFunctionCall
    {
        public required string CallId { get; init; }
        public required string Name { get; init; }
        public required string Arguments { get; init; }
    }

    private class SimulatedFunctionResult
    {
        public required string CallId { get; init; }
        public required string Result { get; init; }
    }
}