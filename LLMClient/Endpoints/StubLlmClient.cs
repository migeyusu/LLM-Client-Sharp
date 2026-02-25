using LLMClient.Abstraction;
using LLMClient.Component;
using LLMClient.Endpoints.OpenAIAPI;
using Microsoft.Extensions.AI;

namespace LLMClient.Endpoints;

public class StubLlmClient : ILLMChatClient
{
    public IEndpointModel Model { get; } = StubLLMChatModel.Instance;
    public IModelParams Parameters { get; set; } = new DefaultModelParam();
    public string Name { get; } = "StubLlmClient";
    public bool IsResponding { get; set; }

    public async Task<CompletedResult> SendRequest(DialogContext context, IInvokeInteractor? interactor = null,
        CancellationToken cancellationToken = default)
    {
#if DEBUG
        interactor ??= new DebugInvokeInteractor();
#endif
        var str = "基于 `TempResponseText` 在 FlowDocument 之外独立显示，`StreamingRenderSession` 不再需要管理任何 tail 文本，结构大幅简化：`livingContainer` 只需包含一个 `RichTextBox`（用于承接子块渲染结果），tail 的显示和清空完全由外部的 `MarkdownTextBlock` + `TempResponseText` 负责。\n\n---\n\n## `StreamingRenderSession.cs`\n\n```csharp\nusing System.Windows;\nusing System.Windows.Controls;\nusing System.Windows.Documents;\nusing System.Windows.Media;\nusing System.Windows.Threading;\nusing Markdig.Syntax;\n\nnamespace LLMClient.Component.Render;\n\npublic sealed class StreamingRenderSession : IDisposable\n{\n    private readonly FlowDocument _mainDocument;\n    private readonly CustomMarkdownRenderer _mainRenderer;\n    private readonly Dispatcher _dispatcher;\n\n    // 当 block commit 时通知外部清空 TempResponseText\n    private readonly Action _clearTail;\n\n    // Living 区域：仅用于承接非顶层已闭合子块\n    private BlockUIContainer? _livingContainer;\n    private FlowDocument? _livingSubDocument;\n    private CustomMarkdownRenderer? _livingSubRenderer;\n    private RichTextBox? _livingSubViewer;\n\n    public StreamingRenderSession(FlowDocument mainDocument, Action clearTail)\n    {\n        _mainDocument = mainDocument;\n        _dispatcher = mainDocument.Dispatcher;\n        _mainRenderer = CustomMarkdownRenderer.NewRenderer(mainDocument);\n        _clearTail = clearTail;\n\n        _dispatcher.Invoke(CreateLivingUI);\n    }\n\n    // -------------------------------------------------------------------------\n    // 后台线程入口（由 StreamParse 的 Closed 回调触发）\n    // -------------------------------------------------------------------------\n\n    public void OnBlockClosed(Block block)\n    {\n        bool isTopLevel = block.Parent is MarkdownDocument;\n\n        // 用 Background 优先级，确保此前所有 Normal 优先级的 TempResponseText.Add\n        // 均已执行完毕后再 commit（Normal > Background，Normal 先执行）\n        _dispatcher.InvokeAsync(() =>\n        {\n            if (isTopLevel)\n                CommitTopLevelBlock(block);\n            else\n                AppendSubBlockToLiving(block);\n\n        }, DispatcherPriority.Background);\n    }\n\n    // -------------------------------------------------------------------------\n    // UI 线程私有操作\n    // -------------------------------------------------------------------------\n\n    private void CreateLivingUI()\n    {\n        _livingSubDocument = new FlowDocument();\n        _livingSubRenderer = CustomMarkdownRenderer.NewRenderer(_livingSubDocument);\n\n        _livingSubViewer = new RichTextBox\n        {\n            Document = _livingSubDocument,\n            IsReadOnly = true,\n            IsDocumentEnabled = true,\n            BorderThickness = new Thickness(0),\n            Padding = new Thickness(0),\n            Background = Brushes.Transparent,\n            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,\n            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,\n            // 没有子块时折叠，不占高度\n            Visibility = Visibility.Collapsed,\n        };\n\n        _livingContainer = new BlockUIContainer(_livingSubViewer);\n        _mainDocument.Blocks.Add(_livingContainer);\n    }\n\n    private void CommitTopLevelBlock(Block block)\n    {\n        // 1. 移除 living 区域（含其中已渲染的子块）\n        if (_livingContainer != null)\n        {\n            _mainDocument.Blocks.Remove(_livingContainer);\n            _livingContainer = null;\n        }\n\n        // 2. 将顶层 block 渲染到主文档\n        _mainRenderer.AppendMarkdownObject(block);\n\n        // 3. 通知外部清空 TempResponseText\n        _clearTail();\n\n        // 4. 重建 living 区域，为下一轮内容做准备\n        CreateLivingUI();\n    }\n\n    private void AppendSubBlockToLiving(Block block)\n    {\n        // 渲染子块到 living 子文档\n        _livingSubRenderer!.AppendMarkdownObject(block);\n\n        // 有内容才显示\n        _livingSubViewer!.Visibility = Visibility.Visible;\n\n        // 清空 tail\n        _clearTail();\n    }\n\n    // -------------------------------------------------------------------------\n    // 生命周期\n    // -------------------------------------------------------------------------\n\n    /// <summary>\n    /// 流结束后调用。使用 ContextIdle 优先级确保所有 Background 级别的\n    /// OnBlockClosed 调度均已执行完毕后再清理空的 livingContainer。\n    /// </summary>\n    public Task CompleteAsync()\n    {\n        return _dispatcher.InvokeAsync(() =>\n        {\n            // 流结束后 living 区域若无子块内容则移除，避免残留空 BlockUIContainer\n            if (_livingContainer != null && _livingSubDocument!.Blocks.Count == 0)\n            {\n                _mainDocument.Blocks.Remove(_livingContainer);\n                _livingContainer = null;\n            }\n        }, DispatcherPriority.ContextIdle).Task;\n        // ContextIdle(3) < Background(4)，确保在所有 Background OnBlockClosed 之后执行\n    }\n\n    public void Dispose()\n    {\n        _dispatcher.InvokeAsync(() =>\n        {\n            if (_livingContainer != null\n                && _mainDocument.Blocks.Contains(_livingContainer))\n            {\n                _mainDocument.Blocks.Remove(_livingContainer);\n            }\n        });\n    }\n}\n```\n\n---\n\n## `ResponseViewItemInteractor`（内部类完整版）\n\n```csharp\nprivate class ResponseViewItemInteractor : BaseViewModel, IInvokeInteractor, IAsyncDisposable\n{\n    private readonly BlockingCollection<string> _blockingCollection = new();\n    private readonly Task _task;\n    private readonly CustomMarkdownRenderer _customRenderer;\n    private readonly StreamingRenderSession _session;\n    private readonly Action<string> _outputAction;\n\n    public ResponseViewItemInteractor(FlowDocument flowDocument, ResponseViewItem responseViewItem)\n    {\n        _customRenderer = CustomMarkdownRenderer.NewRenderer(flowDocument);\n\n        _session = new StreamingRenderSession(\n            flowDocument,\n            clearTail: () => Dispatch(() => responseViewItem.TempResponseText.Clear())\n        );\n\n        _task = Task.Run(() =>\n        {\n            RendererExtensions.StreamParse(\n                _blockingCollection,\n                (_, block) => _session.OnBlockClosed(block));\n        });\n\n        _outputAction = message =>\n        {\n            if (_blockingCollection.IsAddingCompleted) return;\n\n            _blockingCollection.Add(message);\n\n            // Normal 优先级，确保在 Background 级别的 OnBlockClosed 之前执行\n            Dispatch(() =>\n            {\n                responseViewItem.TempResponseText.Add(message);\n                responseViewItem._responseHistory.Append(message);\n            });\n        };\n    }\n\n    public void Info(string message) => _outputAction(message);\n    public void Error(string message) => _outputAction(message);\n    public void Warning(string message) => _outputAction(message);\n    public void Write(string message) => _outputAction(message);\n\n    public void WriteLine(string? message = null)\n    {\n        _outputAction(string.IsNullOrEmpty(message)\n            ? Environment.NewLine\n            : message + Environment.NewLine);\n    }\n\n    public Task<bool> WaitForPermission(string title, string message)\n    {\n        var vm = new PermissionViewModel { Title = title, Content = message };\n        _customRenderer.AppendExpanderItem(vm, CustomMarkdownRenderer.PermissionRequestStyleKey);\n        return vm.Task;\n    }\n\n    public Task<bool> WaitForPermission(object content)\n    {\n        var vm = new PermissionViewModel { Content = content };\n        _customRenderer.AppendExpanderItem(vm, CustomMarkdownRenderer.PermissionRequestStyleKey);\n        return vm.Task;\n    }\n\n    public async ValueTask DisposeAsync()\n    {\n        _blockingCollection.CompleteAdding();\n\n        // 等待 StreamParse 完成（含 CloseAll，所有 Closed 事件均已触发并 dispatch）\n        await _task;\n\n        // 使用 ContextIdle 确保所有 Background 级别的 OnBlockClosed 调度先执行完\n        await _session.CompleteAsync();\n\n        _session.Dispose();\n        _blockingCollection.Dispose();\n    }\n}\n```\n\n---\n\n## 关键设计点说明\n\n**Dispatcher 优先级的有意安排**（这是正确性的核心）：\n\n| 操作 | 优先级 | 原因 |\n|---|---|---|\n| `TempResponseText.Add(message)` | Normal (9) | 需要先于 commit 执行，确保 tail 显示完整 |\n| `OnBlockClosed` dispatch | Background (4) | 晚于 token 追加；确保 tail 文字已显示后再替换 |\n| `CompleteAsync` | ContextIdle (3) | 晚于所有 `OnBlockClosed`；确保清理时 living 区域已最终状态 |\n\nWPF Dispatcher 数值越大优先级越高，同优先级按 FIFO。这三层优先级保证了\"tail 追加 → block commit → 清理\"的严格顺序。";
        if (Parameters.Streaming)
        {
            var random = new Random();
            int currentIndex = 0;
            while (currentIndex < str.Length)
            {
                if (cancellationToken.IsCancellationRequested) break;

                int len = random.Next(3, 6); // 3 to 5 characters
                if (currentIndex + len > str.Length)
                {
                    len = str.Length - currentIndex;
                }

                var chunk = str.Substring(currentIndex, len);
                interactor?.Info(chunk);
                currentIndex += len;

                await Task.Delay(100, cancellationToken);
            }

            return new CompletedResult()
            {
                ResponseMessages = [new ChatMessage(ChatRole.Assistant, str)],
                Usage = new UsageDetails(),
                FinishReason = ChatFinishReason.Stop
            };
        }
        else
        {
            return new CompletedResult()
            {
                ResponseMessages = [new ChatMessage(ChatRole.Assistant, str)],
                Usage = new UsageDetails
                {
                    InputTokenCount = 0,
                    OutputTokenCount = 0,
                    TotalTokenCount = 0,
                    AdditionalCounts = null
                },
                Latency = 0,
                Duration = 0,
                ErrorMessage = null,
                Price = null,
                FinishReason = ChatFinishReason.Stop,
                Annotations = null,
                AdditionalProperties = null
            };
        }
    }

    public ILLMAPIEndpoint Endpoint { get; }
}