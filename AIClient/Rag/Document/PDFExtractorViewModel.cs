using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using LLMClient.Data;

namespace LLMClient.Rag.Document;

public class PDFExtractorViewModel : DocumentExtractorViewModel<PDFNode, PDFPage>
{
    public PDFExtractorViewModel(PDFExtractor extractor, RagOption ragOption, PromptsCache cache) :
        base(ragOption, cache)
    {
        this.Title = "PDF Extractor - Step 1: Select Margin";
        _extractor = extractor;
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

        string[] pages = new string[extractor.PageCount];
        for (int i = 1; i <= extractor.PageCount; i++)
        {
            pages[i - 1] = $"Page {i}";
        }

        Pages = pages;
    }

    public IList<string> Pages { get; }

    private int _selectedPageIndex = 0;

    public int SelectedPageIndex
    {
        get => _selectedPageIndex;
        set
        {
            if (value == _selectedPageIndex) return;
            _selectedPageIndex = value;
            OnPropertyChanged();
            if (value >= 0 && value < Pages.Count)
            {
                CurrentPageNumber = value + 1;
                RenderPage();
            }
        }
    }

    private int _currentStep = 0;

    public override int CurrentStep
    {
        get => _currentStep;
        set
        {
            if (value == _currentStep) return;
            var oldStep = _currentStep;
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
                    if (oldStep < 1)
                    {
                        AnalyzeNode();
                    }

                    break;
                case 2:
                    this.Title = "PDF Extractor - Step 3: Generate Summary";
                    GenerateSummary();
                    break;
            }
        }
    }

    public int CurrentPageNumber { get; private set; } = 1;

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

    public Thickness FileMargin => new(LeftMargin, TopMargin, RightMargin, BottomMargin);

    public List<PDFNode> StructNodes { get; }

    private readonly PDFExtractor _extractor;

    private readonly ConcurrentDictionary<int, IList<PDFNode>> _pageNodeMap = new();

    protected override Func<PDFNode, string> ContextGenerator(int languageIndex)
    {
        return (PDFNode pdfNode) =>
        {
            string context;
            switch (languageIndex)
            {
                case 0:
                    if (pdfNode.HasChildren)
                    {
                        context =
                            $"The text blocks are summary or content of section {pdfNode.Title} in a pdf document.";
                    }
                    else
                    {
                        context =
                            $"The text blocks are raw content of section {pdfNode.Title} in a pdf document. " +
                            $"It's gathered by OCR or text extraction tool, so sentences may be broken in the middle, so you should notice blanks or newline characters first to identify sentence boundaries.";
                    }

                    break;
                case 1:
                    if (pdfNode.HasChildren)
                    {
                        context =
                            $"这些文本块是pdf文档中章节 {pdfNode.Title} 的摘要或内容。";
                    }
                    else
                    {
                        context =
                            $"这些文本块是pdf文档中章节 {pdfNode.Title} 的原始内容。" +
                            $"这些内容是通过OCR或文本提取工具收集的，因此句子可能会在中间断开，你应该首先注意空格或换行符来识别句子的边界。";
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return context;
        };
    }

    private PDFExtractorWindow? _window;

    public void SetWindow(PDFExtractorWindow window)
    {
        _window = window;
    }

    // 核心渲染方法
    public void RenderPage()
    {
        if (_window == null)
        {
            return;
        }

        if (!_window.IsLoaded)
        {
            return;
        }

        var pageChildren = _window.CanvasPage.Children;
        pageChildren.Clear();
        var pageCacheItem = _extractor.Deserialize(_extractor.GetPage(CurrentPageNumber), FileMargin);
        var page = pageCacheItem.Page;
        var pageWidth = page.Width; // PDF点单位
        var pageHeight = page.Height;
        _window.CanvasPage.Width = pageWidth;
        _window.CanvasPage.Height = pageHeight;
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
            var x = boundingBox.TopLeft.X; // PDF左下原点
            var y = pageHeight - boundingBox.TopLeft.Y; // 转换为WPF左上原点
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
            /*foreach (var textLine in block.TextLines)
            {
                var textLineBoundingBox = textLine.BoundingBox;
                var x0 = textLineBoundingBox.BottomLeft.X; // PDF左下原点
                var y0 = pageHeight - textLineBoundingBox.TopRight.Y;
                var fontSize = textLine.Words.FirstOrDefault()?.Letters.FirstOrDefault()?.PointSize;
                var txt = new TextBlock
                {
                    Text = textLine.Text,
                    FontSize = fontSize ?? 10, // 近似；实际从Letters获取字体大小
                    Foreground = Brushes.Black
                };
                Canvas.SetLeft(txt, x0);
                Canvas.SetTop(txt, y0);
                pageChildren.Add(txt);
            }*/
        }

        // 绘制四条margin线（红色虚线）
        DrawMarginLine(_window.CanvasPage, 0, TopMargin, pageWidth, TopMargin); // Top
        DrawMarginLine(_window.CanvasPage, 0, pageHeight - BottomMargin, pageWidth,
            pageHeight - BottomMargin); // Bottom (WPF坐标翻转)
        DrawMarginLine(_window.CanvasPage, LeftMargin, 0, LeftMargin, pageHeight); // Left
        DrawMarginLine(_window.CanvasPage, pageWidth - RightMargin, 0, pageWidth - RightMargin, pageHeight); // Right
        // 绘制Bookmarks
        if (_pageNodeMap.TryGetValue(CurrentPageNumber, out var nodes))
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
        foreach (var letter in page.Letters)
        {
            var lx = letter.StartBaseLine.X;  // 使用基线位置而不是字形边界  
            var ly = pageHeight - letter.StartBaseLine.Y;  // 基线Y坐标  
            var ltxt = new TextBlock  
            {  
                Text = letter.Value,  
                FontSize = letter.PointSize,  
                Foreground = Brushes.Black  
            };  
            Canvas.SetLeft(ltxt, lx);  
            Canvas.SetTop(ltxt, ly);  
            pageChildren.Add(ltxt);  
        }
    }


    // 辅助方法：绘制margin线
    private void DrawMarginLine(Canvas canvas, double x1, double y1, double x2, double y2)
    {
        var line = new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = Brushes.Red,
            StrokeThickness = 2,
            StrokeDashArray = [4, 2] // 虚线
        };
        canvas.Children.Add(line);
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
            ContentNodes = _extractor.Analyze(this.StructNodes);
            IsProcessing = false;
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message);
        }
    }
}