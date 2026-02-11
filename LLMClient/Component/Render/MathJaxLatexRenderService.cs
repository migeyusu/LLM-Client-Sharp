using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using LLMClient.Data;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace LLMClient.Component.Render;

/// <summary>
/// 使用 MathJax 在 WebView2 中渲染 LaTeX 公式，并将其转换为 ImageSource 的服务。
/// </summary>
public class MathJaxLatexRenderService : IDisposable
{
    private readonly WebView2 _webView;

    private readonly TaskCompletionSource<bool> _isWebViewReady = new();

    private Window? _hostWindow;

    private TaskCompletionSource<ImageSource>? _renderTcs;

    private readonly SemaphoreSlim _renderSemaphore = new SemaphoreSlim(1, 1);

    // 缓存已渲染的公式
    private readonly ConcurrentDictionary<string, ImageSource> _cache = new();

    /// <summary>
    /// 异步工厂方法，用于创建和初始化服务。
    /// </summary>
    [STAThread]
    public static async Task<MathJaxLatexRenderService> CreateAsync(WebView2? webView = null)
    {
        var service = webView == null ? new MathJaxLatexRenderService() : new MathJaxLatexRenderService(webView);
        await service.InitializeHostWindow().WaitAsync(TimeSpan.FromSeconds(10));
        return service;
    }

    private static MathJaxLatexRenderService? _instance = null;

    public static async Task<MathJaxLatexRenderService> InstanceAsync()
    {
        _instance ??= await CreateAsync();
        return _instance;
    }

    public static void DisposeInstance()
    {
        _instance?.Dispose();
        _instance = null;
    }

    private MathJaxLatexRenderService(WebView2 webView)
    {
        _webView = webView;
    }

    private MathJaxLatexRenderService()
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


    public async Task<ImageSource> RenderAsync(string latex, double fontSize = 16)
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
            _renderTcs = new TaskCompletionSource<ImageSource>();
            var script = $"renderLatex({JsonSerializer.Serialize(latex)}, {fontSize})";
            await _webView.CoreWebView2.ExecuteScriptAsync(script).WaitAsync(TimeSpan.FromSeconds(5));
            var imageSource = await _renderTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
            _cache.TryAdd(latex, imageSource);
            return imageSource;
        }
        finally
        {
            _renderSemaphore.Release();
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
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
                var s = root.GetProperty("svgDataUrl").GetString();
                if (!string.IsNullOrEmpty(s))
                {
                    var imageSource = ImageExtensions.GetImageSourceFromBase64(s);
                    _renderTcs?.TrySetResult(imageSource);
                }
                else
                {
                    _renderTcs?.TrySetException(new Exception("Failed to get SVG data URL."));
                }

                break;
            }

            case "renderError":
                if (jsonDoc.RootElement.TryGetProperty("snapshot", out var snapshot))
                {
                    Debug.WriteLine($"LaTeX Render Snapshot: {snapshot}");
                }

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
        _hostWindow?.Close();
        _webView?.Dispose();
        _renderSemaphore?.Dispose();
    }
}