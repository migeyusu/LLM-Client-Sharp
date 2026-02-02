using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LLMClient.Dialog;
using Path = System.Windows.Shapes.Path;

namespace LLMClient.Component.Graph;

public partial class DialogGraphControl : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(INavigationViewModel), typeof(DialogGraphControl), 
        new PropertyMetadata(null, OnViewModelChanged));

    public INavigationViewModel ViewModel
    {
        get => (INavigationViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private readonly GraphLayoutEngine _layoutEngine = new();

    public DialogGraphControl()
    {
        InitializeComponent();
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (DialogGraphControl)d;
        control.RefreshGraph();
    }

    // 调用此方法刷新布局
    public void RefreshGraph()
    {
        if (ViewModel?.RootNode == null) return;

        // 1. 计算布局
        var layoutNodes = _layoutEngine.CalculateLayout(ViewModel.RootNode);
        
        // 2. 调整容器大小
        double maxX = layoutNodes.Max(n => n.X + n.Width);
        double maxY = layoutNodes.Max(n => n.Y + n.Height);
        GraphContainer.Width = maxX + 100;
        GraphContainer.Height = maxY + 100;

        // 3. 绑定节点数据
        NodesControl.ItemsSource = layoutNodes;

        // 4. 绘制连线
        DrawConnections(layoutNodes);
    }

    private void DrawConnections(List<GraphNodeViewModel> nodes)
    {
        var mainGeometry = new PathGeometry(); // 灰色主线
        var forkGeometry = new PathGeometry(); // 橙色分叉线

        //创建一个字典方便查找
        var nodeDict = nodes.ToDictionary(n => n.Data.Id, n => n);

        foreach (var node in nodes)
        {
            // 对于每一个节点，找到它的父节点，绘制 父 -> 子 的线
            if (node.Data.PreviousItemId == null) continue;

            if (nodeDict.TryGetValue(node.Data.PreviousItemId.Value, out var parentNode))
            {
                // 起点：父节点底部中心 (也就是 Fork Dot 的位置)
                Point start = parentNode.BottomCenter;
                // 终点：子节点顶部中心
                Point end = node.TopCenter;

                // 判断是否是第一个孩子（主线）
                // 注意：这里需要去原始数据里看它是第几个孩子
                bool isFirstChild = parentNode.Data.Children.FirstOrDefault() == node.Data;

                if (isFirstChild)
                {
                    // 直线 (灰色)
                    var figure = new PathFigure { StartPoint = start, IsClosed = false };
                    figure.Segments.Add(new LineSegment(end, true));
                    mainGeometry.Figures.Add(figure);
                }
                else
                {
                    // 贝塞尔曲线 (橙色)
                    // 控制点计算：模拟 CSS 的 path("M... C x1 y1, x2 y2, ...")
                    // Start (x,y) -> C (x, y+30) (endX, endY-40) -> End
                    
                    Point p1 = new Point(start.X, start.Y + 40); // 向下延伸
                    Point p2 = new Point(end.X, end.Y - 40);     // 终点上方

                    var figure = new PathFigure { StartPoint = start, IsClosed = false };
                    var bezier = new BezierSegment(p1, p2, end, true);
                    figure.Segments.Add(bezier);
                    forkGeometry.Figures.Add(figure);
                }
            }
        }

        // 我们可以用 Group 包含两个 Path，或者为了颜色不同，在 XAML 里放两个 Path
        // 简单起见，我这里只演示了一个 ConnectionsPath，现在我把它们合并渲染
        // 但为了实现颜色区分（主线灰，分叉橙），我们需要两个 Path 元素。
        
        // 修正方案：请在 XAML 中添加第二个 Path x:Name="ForkPath"
        ConnectionsPath.Data = mainGeometry;
        ConnectionsPath.Stroke = (Brush)new BrushConverter().ConvertFrom("#cbd5e1"); // 灰色

        if (this.FindName("ForkPath") is Path forkPath)
        {
            forkPath.Data = forkGeometry;
            forkPath.Stroke = (Brush)new BrushConverter().ConvertFrom("#f59e0b"); // 橙色
        }
    }
}