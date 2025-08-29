using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace LLMClient.Rag.Document;

public partial class PDFExtractorWindow : Window
{
    private readonly PDFExtractorViewModel _viewModel;

    public PDFExtractorWindow(PDFExtractorViewModel viewModel)
    {
        _viewModel = viewModel;
        viewModel.SetWindow(this);
        this.DataContext = _viewModel;
        InitializeComponent();
    }

    private void OK_OnClick(object sender, RoutedEventArgs e)
    {
        this.DialogResult = true;
        this.Close();
    }

    private void PDFExtractorWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        this._viewModel.RenderPage();
    }

    private void RefreshCommandBinding_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is PDFNode node)
        {
            _viewModel.GenerateSummary(node);
        }
    }

    private void DialogHost_OnDialogClosed(object sender, DialogClosedEventArgs eventArgs)
    {
        if (bool.TryParse(eventArgs.Parameter?.ToString(), out var result) && result)
        {
            if (eventArgs.Session.Content is PDFNode pdfNode)
            {
                pdfNode.StartPoint = new Point(pdfNode.StartPointX, pdfNode.StartPointY);
                _viewModel.RenderPage();
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
        }
        else
        {
            if (e.Delta > 0)
            {
                if (_viewModel.SelectedPageIndex > 0)
                {
                    _viewModel.SelectedPageIndex -= 1;
                }
            }
            else
            {
                if (_viewModel.SelectedPageIndex < _viewModel.Pages.Count - 1)
                {
                    _viewModel.SelectedPageIndex += 1;
                }
            }
        }

        e.Handled = true; // 防止事件继续传播
    }

    private void NodeTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        var newValue = e.NewValue;
        if (newValue is PDFNode node)
        {
            if (node.StartPage != _viewModel.CurrentPageNumber)
            {
                _viewModel.SelectedPageIndex = node.StartPage - 1;
            }
        }
    }
}