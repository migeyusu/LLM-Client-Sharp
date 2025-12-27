using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;

namespace LLMClient.Test;

public class ThreadedDocumentHost : HwndHost
{
    private Thread _uiThread;
    private HwndSource _hwndSource;
    private readonly ManualResetEvent _hwndCreated = new ManualResetEvent(false);
    private IntPtr _childHandle = IntPtr.Zero;
    private readonly string _xamlContent;

    // 用于在子线程上延迟加载文档
    private FlowDocumentScrollViewer _viewer;

    public ThreadedDocumentHost(string xamlContent)
    {
        _xamlContent = xamlContent;
    }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        _uiThread = new Thread(() =>
        {
            var parameters = new HwndSourceParameters("ThreadedDocViewer")
            {
                ParentWindow = hwndParent.Handle,
                WindowStyle = 0x40000000 | 0x10000000, // WS_CHILD | WS_VISIBLE
                Width = (int)this.ActualWidth,
                Height = (int)this.ActualHeight
            };

            _hwndSource = new HwndSource(parameters);

            // ===== 关键修改 1：先创建空控件 =====
            _viewer = new FlowDocumentScrollViewer();

            // 显示一个"加载中"的临时文档
            var loadingDoc = new FlowDocument() { Background = Brushes.White };
            loadingDoc.Blocks.Add(new Paragraph(new Run("正在加载文档...")
            {
                FontSize = 16,
                Foreground = Brushes.Gray
            }));
            _viewer.Document = loadingDoc;

            _hwndSource.RootVisual = _viewer;
            _childHandle = _hwndSource.Handle;

            // ===== 关键修改 2：立即通知主线程（不等待文档加载）=====
            _hwndCreated.Set();

            // ===== 关键修改 3：异步加载真实文档内容 =====
            // 使用 Dispatcher.BeginInvoke 确保在子线程的消息循环启动后再加载
            _hwndSource.Dispatcher.BeginInvoke(new Action(() => { LoadDocumentContent(); }),
                System.Windows.Threading.DispatcherPriority.Background);

            // 启动消息循环
            System.Windows.Threading.Dispatcher.Run();
        });
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();

        // 只等待 HWND 创建，不等待文档加载
        _hwndCreated.WaitOne();

        return new HandleRef(this, _childHandle);
    }

    // 在子线程上执行的耗时文档加载
    private void LoadDocumentContent()
    {
        try
        {
            // 这里是耗时操作，但运行在子线程的 Dispatcher 上
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(_xamlContent)))
            {
                var doc = (FlowDocument)XamlReader.Load(stream);
                doc.Background = Brushes.White;
                // 性能优化
                doc.IsOptimalParagraphEnabled = false;
                doc.IsHyphenationEnabled = false;
                doc.PagePadding = new Thickness(20);

                _viewer.Document = doc;
            }
        }
        catch (Exception ex)
        {
            var errorDoc = new FlowDocument();
            errorDoc.Blocks.Add(new Paragraph(new Run($"加载失败: {ex.Message}")
            {
                Foreground = Brushes.Red
            }));
            _viewer.Document = errorDoc;
        }
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        if (_hwndSource != null && !_hwndSource.IsDisposed)
        {
            _hwndSource.Dispatcher.InvokeShutdown();
            _hwndSource.Dispose();
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (_childHandle != IntPtr.Zero)
        {
            MoveWindow(_childHandle, 0, 0,
                (int)sizeInfo.NewSize.Width,
                (int)sizeInfo.NewSize.Height, true);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y,
        int nWidth, int nHeight, bool bRepaint);
}