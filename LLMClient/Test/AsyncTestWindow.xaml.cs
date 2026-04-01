using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace LLMClient.Test;

public partial class AsyncTestWindow : Window, INotifyPropertyChanged
{
    private CancellationTokenSource? _cts;

    /// <summary>
    /// The markdown test string from StubLlmClient — exercises headings, code blocks,
    /// inline code, bold, tables, horizontal rules, lists, and nested structures.
    /// </summary>
    private static readonly string TestMarkdown =
        "基于 `TempResponseText` 在 FlowDocument 之外独立显示，" +
        "`StreamingRenderSession` 不再需要管理任何 **tail 文本**，结构大幅简化：" +
        "`livingContainer` 只需包含一个 `RichTextBox`（用于承接子块渲染结果），" +
        "tail 的显示和清空完全由外部的 `MarkdownTextBlock` + `TempResponseText` 负责。\n\n" +
        "---\n\n" +
        "## `StreamingRenderSession.cs`\n\n" +
        "```csharp\n" +
        "using System.Windows;\n" +
        "using System.Windows.Controls;\n" +
        "using System.Windows.Documents;\n" +
        "using Markdig.Syntax;\n\n" +
        "namespace LLMClient.Component.Render;\n\n" +
        "public sealed class StreamingRenderSession : IDisposable\n" +
        "{\n" +
        "    private readonly FlowDocument _mainDocument;\n" +
        "    private readonly Action _clearTail;\n\n" +
        "    public StreamingRenderSession(FlowDocument doc, Action clearTail)\n" +
        "    {\n" +
        "        _mainDocument = doc;\n" +
        "        _clearTail = clearTail;\n" +
        "    }\n" +
        "}\n" +
        "```\n\n" +
        "## 关键设计点说明\n\n" +
        "**Dispatcher 优先级的有意安排**（这是正确性的核心）：\n\n" +
        "1. `TempResponseText.Add(message)` — Normal (9) 优先级\n" +
        "2. `OnBlockClosed` dispatch — Background (4) 优先级\n" +
        "3. `CompleteAsync` — ContextIdle (3) 优先级\n\n" +
        "> WPF Dispatcher 数值越大优先级越高，同优先级按 FIFO。\n" +
        "> 这三层优先级保证了 *\"tail 追加 → block commit → 清理\"* 的严格顺序。\n\n" +
        "### 特性一览\n\n" +
        "- **粗体** 与 *斜体* 以及 ~~删除线~~\n" +
        "- 行内代码 `var x = 42;`\n" +
        "- [超链接](https://github.com)\n" +
        "- 嵌套列表：\n" +
        "  - 子项 A\n" +
        "  - 子项 B\n\n" +
        "---\n\n" +
        "以上就是完整的流式渲染架构说明。";

    public ObservableCollection<string> StreamingSource { get; } = [];

    public AsyncTestWindow()
    {
        DataContext = this;
        InitializeComponent();
    }

    // ─── Button handlers ──────────────────────────────────────────────

    private async void OnStartClick(object sender, RoutedEventArgs e)
    {
        // Cancel any previous run
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        StreamingSource.Clear();
        StartButton.IsEnabled = false;
        StatusText.Text = "Streaming…";

        try
        {
            await SimulateStreaming(TestMarkdown, token);
            StatusText.Text = $"Done — {TestMarkdown.Length} chars streamed.";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Cancelled.";
        }
        finally
        {
            StartButton.IsEnabled = true;
        }
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        StreamingSource.Clear();
        StatusText.Text = "Ready";
    }

    // ─── Streaming simulation (mirrors StubLlmClient logic) ──────────

    private async Task SimulateStreaming(string text, CancellationToken ct)
    {
        var random = new Random();
        int pos = 0;

        while (pos < text.Length)
        {
            ct.ThrowIfCancellationRequested();

            int len = random.Next(3, 6);
            if (pos + len > text.Length) len = text.Length - pos;

            var chunk = text.Substring(pos, len);
            StreamingSource.Add(chunk);
            pos += len;

            int delay = (int)DelaySlider.Value;
            await Task.Delay(delay, ct);
        }
    }

    // ─── INotifyPropertyChanged ───────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}