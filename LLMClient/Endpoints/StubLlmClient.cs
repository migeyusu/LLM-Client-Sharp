using LLMClient.Abstraction;
using LLMClient.Component;
using LLMClient.Component.Render;
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
        }
    ];

    public async Task<ChatCallResult> SendRequest(RequestContext context, IInvokeInteractor? interactor = null,
        CancellationToken cancellationToken = default)
    {
#if DEBUG
        interactor ??= new DebugInvokeInteractor();
#endif
        IsResponding = true;
        try
        {
            var random = new Random();
            var streaming = Parameters.Streaming;
            var allText = new System.Text.StringBuilder();

            for (int loopIndex = 0; loopIndex < SimulatedLoops.Length; loopIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var loop = SimulatedLoops[loopIndex];

                // ── BeginLoop (与 LlmClientBase 一致) ──
                interactor?.BeginLoop();

                // ── 1. Reasoning / Thinking content ──
                if (!string.IsNullOrEmpty(loop.ThinkingContent))
                {
                    if (streaming)
                    {
                        // 流式输出 <think>...</think>
                        interactor?.Info(ThinkBlockParser.OpenTag + Environment.NewLine);
                        await StreamText(interactor, loop.ThinkingContent, random, cancellationToken);
                        interactor?.Info(Environment.NewLine + ThinkBlockParser.CloseTag + Environment.NewLine);
                    }
                    else
                    {
                        interactor?.WriteLine(ThinkBlockParser.OpenTag);
                        interactor?.WriteLine(loop.ThinkingContent);
                        interactor?.WriteLine(ThinkBlockParser.CloseTag);
                    }
                }

                // ── 2. Text content (LLM 的文本输出) ──
                if (!string.IsNullOrEmpty(loop.TextContent))
                {
                    if (streaming)
                    {
                        await StreamText(interactor, loop.TextContent, random, cancellationToken);
                    }
                    else
                    {
                        interactor?.WriteLine(loop.TextContent);
                    }

                    allText.AppendLine(loop.TextContent);
                }

                // ── 3. Function call (工具调用) ──
                if (loop.FunctionCall != null)
                {
                    var fc = loop.FunctionCall;
                    var functionCallContent = new FunctionCallContent(fc.CallId, fc.Name,
                        new Dictionary<string, object?> { ["raw"] = fc.Arguments });

                    interactor?.WriteLine();
                    interactor?.WriteLine(ToolCallBlockParser.FunctionCallTag);
                    interactor?.WriteLine(functionCallContent.ToToolCallXmlFragment());
                    interactor?.WriteLine(ToolCallBlockParser.FunctionCallEndTag);
                    interactor?.WriteLine();

                    interactor?.Info("Function call detect, need run function calls...");
                    interactor?.WriteLine("Processing function calls...");

                    // 模拟工具执行延迟
                    await Task.Delay(random.Next(500, 1500), cancellationToken);

                    // ── 4. Function result (工具返回) ──
                    if (loop.FunctionResult != null)
                    {
                        var fr = loop.FunctionResult;
                        var functionResultContent = new FunctionResultContent(fr.CallId, fr.Result);

                        interactor?.WriteLine();
                        interactor?.WriteLine(ToolCallResultBlockParser.FunctionResultTag);
                        interactor?.WriteLine(functionResultContent.ToToolCallResultXmlFragment());
                        interactor?.WriteLine(ToolCallResultBlockParser.FunctionResultEndTag);
                        interactor?.WriteLine();
                    }

                    interactor?.WriteLine();
                }
                else
                {
                    // 最后一轮无工具调用
                    interactor?.Info("Response completed without function calls.");
                }
            }

            return new ChatCallResult
            {
                Messages = [new ChatMessage(ChatRole.Assistant, allText.ToString())],
                Usage = new UsageDetails
                {
                    InputTokenCount = 256,
                    OutputTokenCount = 512,
                    TotalTokenCount = 768,
                },
                Latency = 120,
                Duration = 5,
                FinishReason = ChatFinishReason.Stop,
                ValidCallTimes = SimulatedLoops.Length
            };
        }
        finally
        {
            IsResponding = false;
        }
    }

    /// <summary>
    /// 模拟流式输出：每次随机 3-8 个字符，间隔 30-80ms
    /// </summary>
    private static async Task StreamText(IInvokeInteractor? interactor, string text, Random random,
        CancellationToken cancellationToken)
    {
        int currentIndex = 0;
        while (currentIndex < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int len = Math.Min(random.Next(3, 9), text.Length - currentIndex);
            var chunk = text.Substring(currentIndex, len);
            interactor?.Info(chunk);
            currentIndex += len;
            await Task.Delay(random.Next(30, 80), cancellationToken);
        }
    }

    public ILLMAPIEndpoint Endpoint { get; }

    // ── 内部模拟数据结构 ──

    private class ReactLoop
    {
        public string? ThinkingContent { get; init; }
        public string? TextContent { get; init; }
        public SimulatedFunctionCall? FunctionCall { get; init; }
        public SimulatedFunctionResult? FunctionResult { get; init; }
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