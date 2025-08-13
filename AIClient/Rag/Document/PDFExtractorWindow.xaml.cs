using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace LLMClient.Rag.Document;

public partial class PDFExtractorWindow : Window, INotifyPropertyChanged
{
    public int CurrentStep
    {
        get => _currentStep;
        set
        {
            if (value == _currentStep) return;
            _currentStep = value;
            OnPropertyChanged();
            if (value == 1)
            {
                Analyze();
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

    private PDFExtractor _extractor;
    private int _currentStep = 0;
    private IList<PDFContentNode> _contentNodes;

    public PDFExtractorWindow(PDFExtractor extractor)
    {
        _extractor = extractor;
        this.DataContext = this;
        InitializeComponent();
        try
        {
            for (int i = 1; i <= extractor.Document.NumberOfPages; i++)
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

        CanvasPage.Children.Clear();
        var page = _extractor.Document.GetPage(_currentPageNumber);
        var pageWidth = page.Width; // PDF点单位
        var pageHeight = page.Height;

        // 设置Canvas大小（缩放1:1，但可通过ScrollViewer滚动）
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
        CanvasPage.Children.Add(background);

        // 提取Words和TextBlocks（基于工具查询的代码）
        var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters);
        var pageSegmenter = DocstrumBoundingBoxes.Instance;
        var textBlocks = pageSegmenter.GetBlocks(words);

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
            CanvasPage.Children.Add(rect);
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
                CanvasPage.Children.Add(txt);
            }
        }

        // 绘制四条margin线（红色虚线）
        DrawMarginLine(0, TopMargin, pageWidth, TopMargin, true); // Top
        DrawMarginLine(0, pageHeight - BottomMargin, pageWidth, pageHeight - BottomMargin, true); // Bottom (WPF坐标翻转)
        DrawMarginLine(LeftMargin, 0, LeftMargin, pageHeight, false); // Left
        DrawMarginLine(pageWidth - RightMargin, 0, pageWidth - RightMargin, pageHeight, false); // Right
        // 绘制Letters（可选：精确文本渲染；注释掉以简化，但可启用）
        // foreach (var letter in page.Letters)
        // {
        //     double lx = letter.GlyphRectangle.BottomLeft.X;
        //     double ly = pageHeight - letter.GlyphRectangle.TopRight.Y;
        //     TextBlock ltxt = new TextBlock { Text = letter.Value, FontSize = letter.FontSize, Foreground = Brushes.Black };
        //     Canvas.SetLeft(ltxt, lx);
        //     Canvas.SetTop(ltxt, ly);
        //     canvasPage.Children.Add(ltxt);
        // }
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

    public IList<PDFContentNode> ContentNodes
    {
        get => _contentNodes;
        set
        {
            if (Equals(value, _contentNodes)) return;
            _contentNodes = value;
            OnPropertyChanged();
        }
    }

    private void Analyze()
    {
        try
        {
            Thickness margin = this.FileMargin;
            _extractor.Initialize(margin);
            this.ContentNodes = _extractor.Analyze();
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message);
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
}