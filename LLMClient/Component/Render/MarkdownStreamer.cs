using System.Text;
using System.Windows.Documents;
using System.Windows.Threading;
using Markdig;
using Markdig.Syntax;
using Block = System.Windows.Documents.Block;

namespace LLMClient.Component.Render;

public class MarkdownStreamer
{
    private readonly FlowDocument _targetDocument;
    private readonly MarkdownPipeline _pipeline;
    private readonly StringBuilder _accumulator = new();
    private readonly Dispatcher _uiDispatcher;

    public MarkdownStreamer(FlowDocument targetDocument, MarkdownPipeline? pipeline = null)
    {
        _targetDocument = targetDocument;
        _uiDispatcher = targetDocument.Dispatcher; // 获取 UI 线程调度器
        _pipeline = pipeline ?? CustomMarkdownRenderer.DefaultPipeline;
    }

    public void ProcessStream(string token)
    {
        _accumulator.Append(token);
        // 触发 UI 更新
        UpdateUi();
    }

    private void UpdateUi()
    {
        // 1. 在后台线程（或原线程）快速解析出 AST
        var text = _accumulator.ToString();
        var document = Markdown.Parse(text, _pipeline);

        // 2. 在 UI 线程进行增量更新
        _uiDispatcher.Invoke(() => { RenderIncremental(document); });
    }

    private void RenderIncremental(MarkdownDocument ast)
    {
        var uiBlocks = _targetDocument.Blocks;
        int astCount = ast.Count;

        // 如果没有内容，直接返回
        if (astCount == 0) return;

        // 策略：
        // 我们假设 0 到 n-2 的 block 是稳定的（历史对话）。
        // 第 n-1 个 block (最后一个) 是正在生成的（不稳定的）。
        // 因此，我们总是删除 UI 的最后一个 block，然后从 AST 把最后部分重新渲染追加上去。

        // 1. 确定我们要保留多少个 UI Block (安全起见，我们保留 N-1 个)
        // 注意：FlowDocument 的 Blocks 数量应该与上次 AST 的 Blocks 数量一致。
        // 为了防止状态不一致，我们取当前 UI 数量和 AST 数量的较小值，再减去1（作为"脏"块重绘）
        int stableCount = Math.Min(uiBlocks.Count, astCount);
        if (stableCount > 0)
        {
            stableCount--; // 最后一个及其之后的视为不稳定，需要重绘
        }

        // 2. 移除 UI 中不稳定的部分
        while (uiBlocks.Count > stableCount)
        {
            uiBlocks.Remove(uiBlocks.LastBlock);
        }

        // 3. 渲染 AST 中新的部分
        // 这里我们需要一个临时的 FlowDocument 或直接使用 Renderer 渲染特定 Block
        // 由于 CustomMarkdownRenderer 通常是渲染整个 Doc，我们需要手动控制

        var renderer = CustomMarkdownRenderer.NewRenderer(new FlowDocument()); // 创建一个临时的 dummy 文档来承载 renderer 配置
        // 关键：我们要把 renderer 的输出重定向到真实的 uiBlocks
        // 但 Markdig 的设计是 renderer.Render(doc) 会写入 renderer.Object (FlowDocument)
        // 所以我们用临时 Document 渲染，然后把 Block 挪过去

        for (int i = stableCount; i < astCount; i++)
        {
            var block = ast[i];

            // 使用你的自定义渲染器渲染单个 Block
            // 注意：Render(MarkdownObject) 方法通常会在 WpfRenderer 内部处理
            renderer.Render(block);
        }

        // 4. 将渲染出的 Block 从临时文档移动到真实文档
        // FlowDocument 的 Block 不能同时属于两个文档，所以可以直接用 List 转移
        var newBlocks = new List<Block>(renderer.Document!.Blocks);
        renderer.Document.Blocks.Clear(); //虽然没必要，但好习惯

        foreach (var newBlock in newBlocks)
        {
            _targetDocument.Blocks.Add(newBlock);
        }

        // 滚动到底部 (可选)
        // _targetDocument.Parent ... BringIntoView ...
    }
}