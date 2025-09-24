using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using LLMClient.Data;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace LLMClient.UI.Render;

public class LatexRenderService : IDisposable
{
    private readonly WebView2 _webView;

    private readonly TaskCompletionSource<bool> _isWebViewReady = new();

    private Window? _hostWindow;

    private TaskCompletionSource<ImageSource?>? _renderTcs;

    private readonly SemaphoreSlim _renderSemaphore = new SemaphoreSlim(1, 1);

    // 缓存已渲染的公式
    private readonly ConcurrentDictionary<string, ImageSource?> _cache = new();

    /// <summary>
    /// 异步工厂方法，用于创建和初始化服务。
    /// </summary>
    [STAThread]
    public static async Task<LatexRenderService> CreateAsync(WebView2? webView = null)
    {
        var service = webView == null ? new LatexRenderService() : new LatexRenderService(webView);
        await service.InitializeHostWindow();
        return service;
    }

    private LatexRenderService(WebView2 webView)
    {
        _webView = webView;
    }

    private LatexRenderService()
    {
        _hostWindow = new Window
        {
            Width = 0,
            Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            // 将窗口定位到屏幕外，双重保险使其不可见
            Left = -10000,
            Top = -10000,
            Visibility = Visibility.Collapsed
        };
        _webView = new WebView2
        {
            Width = 2048,
            Height = 1024
        };
        _hostWindow.Content = _webView;
    }

    private async Task InitializeHostWindow()
    {
        _hostWindow?.Show();
        _webView.CoreWebView2InitializationCompleted += WebViewOnCoreWebView2InitializationCompleted;
        await _webView.EnsureCoreWebView2Async();
        _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        _webView.NavigateToString(GetHtmlTemplate());
        await _isWebViewReady.Task;
    }

    private void WebViewOnCoreWebView2InitializationCompleted(object? sender,
        CoreWebView2InitializationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            // 设置虚拟主机映射
            var assetsFolderPath = Path.GetFullPath("Resources/Pages/Assets");
            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "assets", // 对应 HTML 中的域名
                assetsFolderPath,
                CoreWebView2HostResourceAccessKind.Allow // 允许访问
            );
        }
    }


    public async Task<ImageSource?> RenderAsync(string latex)
    {
        // 优先从缓存获取
        if (_cache.TryGetValue(latex, out var cachedImage))
        {
            return cachedImage;
        }

        // 等待信号量，确保线程安全和顺序执行
        await _renderSemaphore.WaitAsync();
        try
        {
            _renderTcs = new TaskCompletionSource<ImageSource?>();
            string script = $"renderLatex({JsonSerializer.Serialize(latex)})";
            await _webView.CoreWebView2.ExecuteScriptAsync(script);
            var imageSource = await _renderTcs.Task;
            _cache.TryAdd(latex, imageSource);
            return imageSource;
        }
        finally
        {
            _renderSemaphore.Release();
        }
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var message = e.WebMessageAsJson;
        if (string.IsNullOrEmpty(message)) return;

        var jsonDoc = JsonDocument.Parse(message);
        var messageType = jsonDoc.RootElement.GetProperty("type").GetString();

        switch (messageType)
        {
            case "ready":
                // WebView已加载完毕，可以开始接受渲染任务
                _isWebViewReady.TrySetResult(true);
                break;

            case "svgRenderComplete":
            {
                var root = jsonDoc.RootElement;
                var element = root.GetProperty("svgDataUrl");
                var s = element.GetString();
                
                ImageSource? imageSource;
                if (!string.IsNullOrEmpty(s))
                {
                    imageSource = ImageExtensions.GetImageSourceFromBase64(s);
                }
                else
                {
                    imageSource = null;
                }
                /*var rect = new Rect(
                    root.GetProperty("x").GetDouble(),
                    root.GetProperty("y").GetDouble(),
                    root.GetProperty("width").GetDouble(),
                    root.GetProperty("height").GetDouble()
                );

                // 如果宽高为0，则可能渲染了空内容，直接返回null
                if (rect.Width == 0 || rect.Height == 0)
                {
                    _renderTcs?.TrySetResult(null);
                    return;
                }

                using var stream = new MemoryStream();
                // 核心：使用从JS获取的精确矩形进行截图
                await _webView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
                stream.Position = 0;

                // 将流转换为WPF的BitmapImage
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze(); // 跨线程使用必须冻结*/
                _renderTcs?.TrySetResult(imageSource);
                break;
            }

            case "renderError":
                var errorMessage = jsonDoc.RootElement.GetProperty("message").GetString();
                _renderTcs?.TrySetException(new Exception($"LaTeX render error: {errorMessage}"));
                break;
        }
    }

    private string GetHtmlTemplate()
    {
        var uri = new Uri("pack://application:,,,/LLMClient;component/Resources/Pages/render_latex_template.html");
        var resourceStream = Application.GetResourceStream(uri);
        if (resourceStream == null)
        {
            throw new FileNotFoundException("Resource not found", uri.ToString());
        }

        using (var streamReader = new StreamReader(resourceStream.Stream))
        {
            return streamReader.ReadToEnd();
        }
    }

    public void Dispose()
    {
        _webView?.Dispose();
        _renderSemaphore?.Dispose();
    }
}