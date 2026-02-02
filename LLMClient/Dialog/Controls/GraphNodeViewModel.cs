using System.Collections.ObjectModel;
using System.Windows;
using LLMClient.Component.ViewModel.Base;

// 引用你的数据结构命名空间

namespace LLMClient.Dialog.Controls;

// 节点的显示模型
public class GraphNodeViewModel : BaseViewModel
{
    public IDialogItem Data { get; }

    // 坐标与尺寸
    private double _x;
    public double X { get => _x; set { SetField(ref _x, value); UpdateCenter(); } }

    private double _y;
    public double Y { get => _y; set { SetField(ref _y, value); UpdateCenter(); } }

    public double Width { get; set; } = 260; // 对应 CSS --node-w
    public double Height { get; set; } = 100; // 预估高度，实际可由Measure重算

    // 辅助属性
    public bool IsUser => Data is BaseDialogItem b && b.Role == Microsoft.Extensions.AI.ChatRole.User;
    
    // 只有当有子节点时才显示底部的橙色圆点
    public bool ShowForkDot => Data.Children.Count > 0;

    // 底部中心点，用于连线发射源
    public Point BottomCenter { get; private set; }

    public GraphNodeViewModel(IDialogItem data)
    {
        Data = data;
    }

    private void UpdateCenter()
    {
        BottomCenter = new Point(X + Width / 2, Y + Height);
    }
}

// 连线的显示模型
public class GraphConnectionViewModel : BaseViewModel
{
    // 是否是主分支连线（决定颜色：灰色 vs 橙色）
    public bool IsMainBranch { get; set; }
    
    // SVG 路径数据字符串
    public string PathData { get; set; } = string.Empty;
}

public class GraphLayoutEngine
{
    // 配置参数 (对应 CSS 变量)
    private const double ColGap = 80;
    private const double NodeGap = 60;
    private const double NodeWidth = 260;
    
    // 简单的文本高度估算器 (实际项目中可以使用 FormattedText.Measure)
    private double EstimateHeight(string text)
    {
        if (string.IsNullOrEmpty(text)) return 60;
        int lines = (text.Length / 30) + 1; 
        return Math.Max(60, 40 + lines * 20);
    }

    public void Layout(
        IDialogItem root, 
        ObservableCollection<GraphNodeViewModel> nodes, 
        ObservableCollection<GraphConnectionViewModel> connections)
    {
        nodes.Clear();
        connections.Clear();

        if (root == null) return;

        // 1. 包装节点并构建查找表
        var wrapperMap = new Dictionary<Guid, GraphNodeViewModel>();
        
        // 递归包装
        WrapNode(root, wrapperMap, nodes);

        // 2. 计算位置 (DFS)
        // columnY 记录每一列当前占用的最大 Y 值，防止垂直重叠
        var columnY = new Dictionary<int, double>();
        
        // 递归布局
        PlaceNode(root, wrapperMap, 0, columnY);

        // 3. 生成连线
        foreach (var node in nodes)
        {
            if (node.Data.Children == null) continue;

            int childIndex = 0;
            foreach (var child in node.Data.Children)
            {
                if (wrapperMap.TryGetValue(child.Id, out var childNode))
                {
                    var isMain = childIndex == 0; // 第一个孩子视为主分支
                    var path = CalculateBezierPath(node.BottomCenter, new Point(childNode.X + childNode.Width / 2, childNode.Y));
                    
                    connections.Add(new GraphConnectionViewModel
                    {
                        IsMainBranch = isMain,
                        PathData = path
                    });
                }
                childIndex++;
            }
        }
    }

    private void WrapNode(IDialogItem item, Dictionary<Guid, GraphNodeViewModel> map, ObservableCollection<GraphNodeViewModel> list)
    {
        var vm = new GraphNodeViewModel(item);
        // 估算高度
        if (item is BaseDialogItem baseItem)
        {
            vm.Height = EstimateHeight(baseItem.DisplayText);
        }
        
        map[item.Id] = vm;
        list.Add(vm);

        foreach (var child in item.Children)
        {
            WrapNode(child, map, list);
        }
    }

    // 核心递归布局算法
    private void PlaceNode(IDialogItem item, Dictionary<Guid, GraphNodeViewModel> map, int colIndex, Dictionary<int, double> columnY)
    {
        var vm = map[item.Id];

        // 确定 X 坐标
        vm.X = 20 + colIndex * (NodeWidth + ColGap);

        // 确定 Y 坐标
        // 取当前列的可用 Y，或者如果是根节点/分支起点，需要考虑父节点的 Y
        double startY = 20;
        if (columnY.TryGetValue(colIndex, out double val))
        {
            startY = val;
        }

        // 如果有父节点，且父节点不仅是逻辑上的父节点，还是视觉上的上级
        //我们需要确保 Y 至少在父节点下方
        if (item.PreviousItem != null && map.TryGetValue(item.PreviousItem.Id, out var parentVm))
        {
            // 确保不低于父节点底部 + 间隙
            startY = Math.Max(startY, parentVm.Y + parentVm.Height + NodeGap);
        }

        vm.Y = startY;

        // 更新该列的占用情况
        columnY[colIndex] = vm.Y + vm.Height + NodeGap; // 下一个节点预留位置

        // 处理子节点
        int childIdx = 0;
        foreach (var child in item.Children)
        {
            // 第一个孩子继承当前列 (主干)
            // 后续孩子向右寻找新列
            int targetCol = (childIdx == 0) ? colIndex : FindNextAvailableColumn(colIndex + 1, columnY, vm.Y + vm.Height + NodeGap);

            // 递归
            PlaceNode(child, map, targetCol, columnY);
            childIdx++;
        }
    }

    // 寻找从 layoutStartCol 开始，在指定 yPos 高度空闲的列
    private int FindNextAvailableColumn(int startCol, Dictionary<int, double> columnY, double yPos)
    {
        int col = startCol;
        while (true)
        {
            if (!columnY.ContainsKey(col) || columnY[col] <= yPos + 10) // +10容差
            {
                return col;
            }
            col++;
        }
    }

    // 计算类似 CSS 的贝塞尔曲线
    // 起点统一为 Parent Bottom Center
    private string CalculateBezierPath(Point start, Point end)
    {
        // 逻辑：
        // Start -> 向下延伸一段 (垂直)
        // End -> 向上延伸一段 (垂直，如果跨列则是水平切入的过渡)
        
        // 简单的 S 形曲线逻辑
        // Control Point 1: Start 下方 NodeGap/2 处
        // Control Point 2: End 上方 NodeGap/2 处 (但是 X 轴对齐 End)
        
        double cp1Y = start.Y + (NodeGap / 2);
        double cp2Y = end.Y - (NodeGap / 2);

        // 如果是同列直线，CP 直接在直线上
        // 如果是跨列，X 需要插值
        
        // 构造 Path 字符串: M startX,startY C cp1X,cp1Y cp2X,cp2Y endX,endY
        return $"M {start.X},{start.Y} C {start.X},{cp1Y} {end.X},{cp2Y} {end.X},{end.Y}";
    }
}