using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Markdig.Syntax;
using Block = Markdig.Syntax.Block;

namespace LLMClient.Component.Render;

public sealed class StreamingRenderSession : IDisposable
{
    private readonly FlowDocument _mainDocument;
    private readonly CustomMarkdownRenderer _mainRenderer;
    private readonly Dispatcher _dispatcher;

    // 当 block commit 时通知外部清空 TempResponseText
    private readonly Action _clearTail;

    // Living 区域：仅用于承接非顶层已闭合子块
    private BlockUIContainer? _livingContainer;
    private FlowDocument? _livingSubDocument;
    private CustomMarkdownRenderer? _livingSubRenderer;
    private RichTextBox? _livingSubViewer;

    public StreamingRenderSession(FlowDocument mainDocument, Action clearTail)
    {
        _mainDocument = mainDocument;
        _dispatcher = mainDocument.Dispatcher;
        _mainRenderer = CustomMarkdownRenderer.NewRenderer(mainDocument);
        _clearTail = clearTail;

        _dispatcher.Invoke(CreateLivingUI);
    }

    // -------------------------------------------------------------------------
    // 后台线程入口（由 StreamParse 的 Closed 回调触发）
    // -------------------------------------------------------------------------

    public void OnBlockClosed(Block block)
    {
        bool isTopLevel = block.Parent is MarkdownDocument;

        // 用 Background 优先级，确保此前所有 Normal 优先级的 TempResponseText.Add
        // 均已执行完毕后再 commit（Normal > Background，Normal 先执行）
        _dispatcher.InvokeAsync(() =>
        {
            if (isTopLevel)
                CommitTopLevelBlock(block);
            else
                AppendSubBlockToLiving(block);

        }, DispatcherPriority.Background);
    }

    // -------------------------------------------------------------------------
    // UI 线程私有操作
    // -------------------------------------------------------------------------

    private void CreateLivingUI()
    {
        _livingSubDocument = new FlowDocument();
        _livingSubRenderer = CustomMarkdownRenderer.NewRenderer(_livingSubDocument, enableTextMate: false);

        _livingSubViewer = new RichTextBox
        {
            Document = _livingSubDocument,
            IsReadOnly = true,
            IsDocumentEnabled = true,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            // 没有子块时折叠，不占高度
            Visibility = Visibility.Collapsed,
        };

        _livingContainer = new BlockUIContainer(_livingSubViewer);
        _mainDocument.Blocks.Add(_livingContainer);
    }

    private void CommitTopLevelBlock(Block block)
    {
        // 1. 移除 living 区域（含其中已渲染的子块）
        if (_livingContainer != null)
        {
            _mainDocument.Blocks.Remove(_livingContainer);
            _livingContainer = null;
        }

        // 2. 将顶层 block 渲染到主文档
        _mainRenderer.AppendMarkdownObject(block);

        // 3. 通知外部清空 TempResponseText
        _clearTail();

        // 4. 重建 living 区域，为下一轮内容做准备
        CreateLivingUI();
    }

    private void AppendSubBlockToLiving(Block block)
    {
        // 渲染子块到 living 子文档
        _livingSubRenderer!.AppendMarkdownObject(block);

        // 有内容才显示
        _livingSubViewer!.Visibility = Visibility.Visible;

        // 清空 tail
        _clearTail();
    }

    // -------------------------------------------------------------------------
    // 生命周期
    // -------------------------------------------------------------------------

    /// <summary>
    /// 流结束后调用。使用 ContextIdle 优先级确保所有 Background 级别的
    /// OnBlockClosed 调度均已执行完毕后再清理空的 livingContainer。
    /// </summary>
    public Task CompleteAsync()
    {
        return _dispatcher.InvokeAsync(() =>
        {
            // 流结束后 living 区域若无子块内容则移除，避免残留空 BlockUIContainer
            if (_livingContainer != null && _livingSubDocument!.Blocks.Count == 0)
            {
                _mainDocument.Blocks.Remove(_livingContainer);
                _livingContainer = null;
            }
        }, DispatcherPriority.ContextIdle).Task;
        // ContextIdle(3) < Background(4)，确保在所有 Background OnBlockClosed 之后执行
    }

    public void Dispose()
    {
        _dispatcher.InvokeAsync(() =>
        {
            if (_livingContainer != null
                && _mainDocument.Blocks.Contains(_livingContainer))
            {
                _mainDocument.Blocks.Remove(_livingContainer);
            }
        });
    }
}