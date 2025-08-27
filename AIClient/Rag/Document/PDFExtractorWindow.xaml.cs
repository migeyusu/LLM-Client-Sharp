using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using LLMClient.Data;
using LLMClient.UI.Component;
using LLMClient.UI.Log;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace LLMClient.Rag.Document;

public partial class PDFExtractorWindow : Window, INotifyPropertyChanged
{
    private int _currentStep = 0;

    public int CurrentStep
    {
        get => _currentStep;
        set
        {
            if (value == _currentStep) return;
            _currentStep = value;
            OnPropertyChanged();
            switch (value)
            {
                case 0:
                    this.Title = "PDF Extractor - Step 1: Select Margin";
                    break;
                // 根据步骤执行不同的操作
                case 1:
                    this.Title = "PDF Extractor - Step 2: Analyze Content";
                    AnalyzeNode();
                    break;
                case 2:
                    this.Title = "PDF Extractor - Step 3: Generate Summary";
                    GenerateSummary();
                    break;
            }
        }
    }

    private int _currentPageNumber = 1;
    private double _topMargin = 50d;
    private double _bottomMargin = 50d;
    private double _leftMargin = 50d;
    private double _rightMargin = 50d;

    public double TopMargin
    {
        get => _topMargin;
        set
        {
            if (value.Equals(_topMargin)) return;
            _topMargin = value;
            OnPropertyChanged();
            RenderPage();
        }
    }

    public double BottomMargin
    {
        get => _bottomMargin;
        set
        {
            if (value.Equals(_bottomMargin)) return;
            _bottomMargin = value;
            OnPropertyChanged();
            RenderPage();
        }
    }

    public double LeftMargin
    {
        get => _leftMargin;
        set
        {
            if (value.Equals(_leftMargin)) return;
            _leftMargin = value;
            OnPropertyChanged();
            RenderPage();
        }
    }

    public double RightMargin
    {
        get => _rightMargin;
        set
        {
            if (value.Equals(_rightMargin)) return;
            _rightMargin = value;
            OnPropertyChanged();
            RenderPage();
        }
    }

    public Thickness FileMargin
    {
        get => new Thickness(LeftMargin, TopMargin, RightMargin, BottomMargin);
    }

    private int _summaryLanguageIndex;

    public int SummaryLanguageIndex
    {
        get => _summaryLanguageIndex;
        set
        {
            if (value == _summaryLanguageIndex) return;
            _summaryLanguageIndex = value;
            OnPropertyChanged();
        }
    }

    private IList<PDFNode> _contentNodes = Array.Empty<PDFNode>();

    public IList<PDFNode> ContentNodes
    {
        get => _contentNodes;
        set
        {
            if (Equals(value, _contentNodes)) return;
            _contentNodes = value;
            OnPropertyChanged();
        }
    }

    private bool _isProcessing;

    public bool IsProcessing
    {
        get => _isProcessing;
        set
        {
            if (value == _isProcessing) return;
            _isProcessing = value;
            OnPropertyChanged();
        }
    }

    private double _progressValue;

    public double ProgressValue
    {
        get => _progressValue;
        set
        {
            if (value.Equals(_progressValue)) return;
            _progressValue = value;
            OnPropertyChanged();
        }
    }

    public LogsViewModel Logs { get; set; } = new LogsViewModel();

    public List<PDFNode> StructNodes { get; }

    private readonly PDFExtractor _extractor;

    private ConcurrentDictionary<int, IList<PDFNode>> _pageNodeMap = new();

    public PDFExtractorWindow(PDFExtractor extractor, RagOption ragOption)
    {
        _extractor = extractor;
        this._ragOption = ragOption;
        this.StructNodes = extractor.ExtractTree();
        foreach (var structNode in StructNodes.Flatten())
        {
            _pageNodeMap.AddOrUpdate(structNode.StartPage, new List<PDFNode> { structNode },
                (key, list) =>
                {
                    list.Add(structNode);
                    return list;
                });
        }

        this.DataContext = this;
        InitializeComponent();
        try
        {
            for (int i = 1; i <= extractor.PageCount; i++)
            {
                CmbPages.Items.Add($"Page {i}");
            }

            CmbPages.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载失败: {ex.Message}");
        }
    }


    // 页面选择变化事件
    private void Page_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbPages.SelectedIndex >= 0)
        {
            _currentPageNumber = CmbPages.SelectedIndex + 1;
            RenderPage();
        }
    }


    // 核心渲染方法
    private void RenderPage()
    {
        if (!this.IsLoaded)
        {
            return;
        }

        var pageChildren = CanvasPage.Children;
        pageChildren.Clear();
        var pageCacheItem = _extractor.Deserialize(_extractor.GetPage(_currentPageNumber), FileMargin);
        var page = pageCacheItem.Page;
        var pageWidth = page.Width; // PDF点单位
        var pageHeight = page.Height;
        CanvasPage.Width = pageWidth;
        CanvasPage.Height = pageHeight;
        // 绘制页面背景（白色矩形）
        var background = new Rectangle
        {
            Width = pageWidth,
            Height = pageHeight,
            Fill = Brushes.White,
            Stroke = Brushes.Black,
            StrokeThickness = 1
        };
        Canvas.SetLeft(background, 0);
        Canvas.SetTop(background, 0);
        pageChildren.Add(background);
        var textBlocks = pageCacheItem.RemainingTextBlocks;
        // 绘制每个TextBlock的文本和边界框
        foreach (var block in textBlocks)
        {
            var boundingBox = block.BoundingBox;
            var x = boundingBox.BottomLeft.X; // PDF左下原点
            var y = pageHeight - boundingBox.TopRight.Y; // 转换为WPF左上原点
            var width = boundingBox.Width;
            var height = boundingBox.Height;

            // 绘制边界框（蓝色，如果排除则红色）
            var rect = new Rectangle
            {
                Width = width,
                Height = height,
                Stroke = Brushes.Blue,
                StrokeThickness = 1,
                Opacity = 1.0 // 排除时半透明
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            pageChildren.Add(rect);
            foreach (var textLine in block.TextLines)
            {
                var textLineBoundingBox = textLine.BoundingBox;
                var x0 = textLineBoundingBox.BottomLeft.X; // PDF左下原点
                var y0 = pageHeight - textLineBoundingBox.TopRight.Y;
                var fontSize = textLine.Words.FirstOrDefault()?.Letters.FirstOrDefault()?.FontSize;
                var txt = new TextBlock
                {
                    Text = textLine.Text,
                    FontSize = fontSize ?? 10, // 近似；实际从Letters获取字体大小
                    Foreground = Brushes.Black
                };
                Canvas.SetLeft(txt, x0);
                Canvas.SetTop(txt, y0);
                pageChildren.Add(txt);
            }
        }

        // 绘制四条margin线（红色虚线）
        DrawMarginLine(0, TopMargin, pageWidth, TopMargin, true); // Top
        DrawMarginLine(0, pageHeight - BottomMargin, pageWidth, pageHeight - BottomMargin, true); // Bottom (WPF坐标翻转)
        DrawMarginLine(LeftMargin, 0, LeftMargin, pageHeight, false); // Left
        DrawMarginLine(pageWidth - RightMargin, 0, pageWidth - RightMargin, pageHeight, false); // Right
        // 绘制Bookmarks
        if (_pageNodeMap.TryGetValue(_currentPageNumber, out var nodes))
        {
            foreach (var node in nodes)
            {
                var point = node.StartPoint;
                var txt = new TextBlock
                {
                    Text = node.Title,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Green
                };
                Canvas.SetLeft(txt, point.X); // 靠左显示
                Canvas.SetTop(txt, pageHeight - point.Y);
                pageChildren.Add(txt);
                //add a crosshair
                var line1 = new Line
                {
                    X1 = point.X - 5, Y1 = pageHeight - point.Y, X2 = point.X + 5, Y2 = pageHeight - point.Y,
                    Stroke = Brushes.Red, StrokeThickness = 1
                };
                var line2 = new Line
                {
                    X1 = point.X, Y1 = pageHeight - point.Y - 5, X2 = point.X, Y2 = pageHeight - point.Y + 5,
                    Stroke = Brushes.Red, StrokeThickness = 1
                };
                pageChildren.Add(line1);
                pageChildren.Add(line2);
            }
        }

        //绘制Letters（可选：精确文本渲染；注释掉以简化，但可启用）
        /*foreach (var letter in page.Letters)
        {
            double lx = letter.GlyphRectangle.BottomLeft.X;
            double ly = pageHeight - letter.GlyphRectangle.TopRight.Y;
            TextBlock ltxt = new TextBlock { Text = letter.Value, FontSize = letter.FontSize, Foreground = Brushes.Black };
            Canvas.SetLeft(ltxt, lx);
            Canvas.SetTop(ltxt, ly);
            CanvasPage.Children.Add(ltxt);
        }*/
    }

    // 辅助方法：绘制margin线
    private void DrawMarginLine(double x1, double y1, double x2, double y2, bool isHorizontal)
    {
        var line = new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = Brushes.Red,
            StrokeThickness = 2,
            StrokeDashArray = [4, 2] // 虚线
        };
        CanvasPage.Children.Add(line);
    }

    private async void AnalyzeNode()
    {
        try
        {
            Thickness margin = this.FileMargin;
            var pages = _extractor.PageCount;
            var progress = new Progress<int>(i => this.ProgressValue = i / (double)pages);
            IsProcessing = true;
            await Task.Run(() => { _extractor.Initialize(progress, margin); });
            this.ContentNodes = _extractor.Analyze(this.StructNodes);
            IsProcessing = false;
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message);
        }
    }

    private PromptsCache? _promptsCache;

    private readonly RagOption _ragOption;

    private async void GenerateSummary()
    {
        var digestClient = _ragOption.DigestClient;
        if (digestClient == null)
        {
            throw new InvalidOperationException("Digest client is not set.");
        }

        int nodeCount = 0;
        foreach (var contentNode in this.ContentNodes)
        {
            nodeCount += contentNode.CountRecursive();
        }

        int progressCount = 0;
        var progress = new Progress<PDFNode>(node =>
        {
            progressCount++;
            this.ProgressValue = ((double)progressCount) / nodeCount;
            // 会自动在UI线程调用
            Logs.LogInformation("Processing node {0}, start page: {1}, level: {2}",
                node.Title, node.StartPage, node.Level);
        });
        using (var semaphoreSlim = new SemaphoreSlim(5, 5))
        {
            var summarySize = Extension.SummarySize;
            _promptsCache ??= new PromptsCache(Guid.NewGuid().ToString(), PromptsCache.CacheFolderPath,
                digestClient.Endpoint.Name, digestClient.Model.Id) { OutputSize = summarySize };
            try
            {
                Logs.Start();
                IsProcessing = true;
                // await promptsCache.InitializeAsync();
                /*async (s, cancellationToken) =>
                        {
                            await Task.Delay(1000, cancellationToken);
                            var length = s.Length;
                            return s.Substring(0, int.Min(length, 1000));
                        }*/
                var summaryDelegate =
                    digestClient.CreateSummaryDelegate(semaphoreSlim, SummaryLanguageIndex, _promptsCache,
                        logger: this.Logs,
                        summarySize: summarySize, retryCount: 3);
                await Parallel.ForEachAsync(this.ContentNodes, new ParallelOptions(),
                    async (node, token) =>
                    {
                        await node.GenerateSummarize<PDFNode, PDFPage>(summaryDelegate, this.Logs, progress,
                            token: token);
                    });
                MessageEventBus.Publish("Summary generated successfully!");
            }
            catch (Exception e)
            {
                // await promptsCache.SaveAsync();
                MessageBox.Show($"Failed to generate summary: {e.Message}");
            }
            finally
            {
                IsProcessing = false;
                Logs.Stop();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void OK_OnClick(object sender, RoutedEventArgs e)
    {
        this.DialogResult = true;
        this.Close();
    }

    private void PDFExtractorWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        this.RenderPage();
    }

    private async void RefreshCommandBinding_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is MarkdownNode node)
        {
            try
            {
                IsProcessing = true;
                var digestClient = _ragOption.DigestClient;
                if (digestClient == null)
                {
                    throw new InvalidOperationException("Digest client is not set.");
                }

                using (var semaphoreSlim = new SemaphoreSlim(1, 1))
                {
                    var summarySize = Extension.SummarySize;
                    var summaryDelegate =
                        digestClient.CreateSummaryDelegate(semaphoreSlim, SummaryLanguageIndex,
                            PromptsCache.NoCache, logger: this.Logs,
                            summarySize: summarySize, retryCount: 3);
                    using (var source = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
                    {
                        var summaryRaw = node.SummaryRaw;
                        node.Summary = await summaryDelegate(summaryRaw, source.Token);
                        _promptsCache?.AddOrUpdate(summaryRaw, node.Summary);
                    }
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }
            finally
            {
                IsProcessing = false;
            }
        }
    }

    private void ClearCache_OnClick(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("是否清除缓存？", "确认", MessageBoxButton.OKCancel, MessageBoxImage.Question)
            == MessageBoxResult.OK)
        {
            this._promptsCache?.Clear();
        }
    }

    private void DialogHost_OnDialogClosed(object sender, DialogClosedEventArgs eventArgs)
    {
        if (bool.TryParse(eventArgs.Parameter?.ToString(), out var result) && result)
        {
            if (eventArgs.Session.Content is PDFNode pdfNode)
            {
                pdfNode.StartPoint = new Point(pdfNode.StartPointX, pdfNode.StartPointY);
                RenderPage();
            }
        }
    }

    private void CanvasPage_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            // 获取鼠标相对于ScrollViewer的位置
            Point mousePosition = e.GetPosition(ScrollViewerPage);

            // 获取或创建ScaleTransform
            var scaleTransform = CanvasPage.LayoutTransform as ScaleTransform;
            if (scaleTransform == null)
            {
                scaleTransform = new ScaleTransform(1.0, 1.0);
                CanvasPage.LayoutTransform = scaleTransform;
            }

            // 计算当前缩放比例
            double currentScale = scaleTransform.ScaleX;

            // 计算缩放因子
            double zoomFactor = e.Delta > 0 ? 1.1 : 0.9;
            double newScale = currentScale * zoomFactor;

            // 计算内容在缩放前后的位置差异，并调整滚动位置
            double horizontalOffset = ScrollViewerPage.HorizontalOffset;
            double verticalOffset = ScrollViewerPage.VerticalOffset;

            // 应用新的缩放
            scaleTransform.ScaleX = newScale;
            scaleTransform.ScaleY = newScale;

            // 调整滚动位置，使鼠标位置下的内容保持不变
            ScrollViewerPage.ScrollToHorizontalOffset(
                horizontalOffset * zoomFactor + mousePosition.X * (zoomFactor - 1));
            ScrollViewerPage.ScrollToVerticalOffset(verticalOffset * zoomFactor + mousePosition.Y * (zoomFactor - 1));

            e.Handled = true; // 防止事件继续传播
        }
    }

    private void NodeTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        var newValue = e.NewValue;
        if (newValue is PDFNode node)
        {
            if (node.StartPage != _currentPageNumber)
            {
                CmbPages.SelectedIndex = node.StartPage - 1;
            }
        }
    }
}