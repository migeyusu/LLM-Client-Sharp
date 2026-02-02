using System.Globalization;
using System.Windows;
using System.Windows.Media;
using LLMClient.Dialog;

namespace LLMClient.Component.Graph;

public class GraphLayoutEngine
{
    private const double ColWidth = 260;
    private const double ColGap = 80;
    private const double NodeGapY = 60;
    private const double PaddingX = 20;
    private const double PaddingY = 20;

    // 估算文本高度的辅助工具
    private static double EstimateHeight(string text, double width)
    {
        if (string.IsNullOrEmpty(text)) return 60;
        
        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var formattedText = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            13, // FontSize
            Brushes.Black,
            1.25 // PixelsPerDip
        )
        {
            MaxTextWidth = width - 28, // Padding left/right 14+14
            Trimming = TextTrimming.WordEllipsis
        };

        // 加上 Padding Top/Bottom (14+14) + Label Height(20) + Buffer
        return formattedText.Height + 50; 
    }

    public List<GraphNodeViewModel> CalculateLayout(IDialogItem root)
    {
        var allNodes = new List<GraphNodeViewModel>();
        var colTracker = new Dictionary<int, double>(); // 记录每列当前的 Y 坐标

        // 递归构建
        Traverse(root, 0, PaddingY, allNodes, colTracker);

        return allNodes;
    }

    private void Traverse(
        IDialogItem item, 
        int colIndex, 
        double startY, 
        List<GraphNodeViewModel> resultList, 
        Dictionary<int, double> colTracker)
    {
        // 1. 创建 ViewModel
        var viewModel = new GraphNodeViewModel(item);
        
        // 2. 计算高度 (简单估算，或者可以通过 Measure 实现更精确)
        string text = (item as BaseDialogItem)?.DisplayText ?? "";
        viewModel.Height = Math.Max(60, EstimateHeight(text, viewModel.Width));
        
        // 3. 确定位置
        // X 坐标基于列索引
        viewModel.X = PaddingX + colIndex * (ColWidth + ColGap);
        
        // Y 坐标：取 (父节点的底部 + 间距) 和 (当前列已占用的底部) 的最大值
        // 实际上在对话流中，通常是紧接着父节点，或者如果是兄弟节点，则与兄弟齐平
        // 这里简化逻辑：如果是第一个子节点，紧接父节点；如果是后续分支，尝试与兄弟主要对齐，但避开重叠
        
        // 简单策略：如果不冲突，就放在 startY，如果冲突就往下推 (这里简化为直接使用 startY，因为我们会控制 Column)
        viewModel.Y = startY;

        // 更新当前列的占用情况
        if (colTracker.TryGetValue(colIndex, out double currentMaxY))
        {
            if (viewModel.Y < currentMaxY + NodeGapY)
            {
                // 如果计算出的位置比当前列的堆积位置还高（说明回溯了），可能需要处理。
                // 但在这个自上而下的树中，我们通常一直往下走。
                // 只有并行分支才会共享 Y start。
            }
        }
        colTracker[colIndex] = viewModel.Y + viewModel.Height;

        // 设置视觉属性
        viewModel.HasFork = item.Children.Count > 1; // 只有多于一个孩子才显示分叉点？或者只要有孩子且需要连线？
        // 根据你的设计，ForkDot在父节点底部。
        // 子节点不需要知道自己是否有ForkDot，而是父节点 ViewModel 需要知道。
        // 这是一个后处理：
        if (resultList.Count > 0)
        {
            // 上一个添加的如果是父节点... 不好判断，直接在循环外处理
        }
        
        resultList.Add(viewModel);

        // 4. 处理子节点
        if (item.Children.Count == 0) return;

        var children = item.Children.ToList();
        
        // 第一个孩子由主干继承 (Same Column)
        // 它的 Y 起点是 当前节点 Y + Height + Gap
        double nextY = viewModel.Y + viewModel.Height + NodeGapY;
        
        Traverse(children[0], colIndex, nextY, resultList, colTracker);

        // 后续孩子 (分支) 向右寻找新列
        // 这里的逻辑：每个后续分支开启一个新的列，且初始 Y 与第一个孩子对齐
        int currentBranchCol = colIndex + 1; 

        for (int i = 1; i < children.Count; i++)
        {
            // 简单算法：分支直接占下一个可用列。
            // 为了防止深层嵌套重叠，实际需要更复杂的分配，这里假设向右直接累加
            // 更好的做法是传递一个 ref maxColIndex
            
            // 寻找一个在 nextY 高度未被占用的列 (简化：直接 +1，实际应用可能需要全局列管理器)
            // 在此Demo中我们假设每个分支向右偏移一列，但需要注意子树宽度
            int branchCol = GetRightmostColumn(resultList) + 1; 
            
            // 分支的起始 Y 应该和第一个孩子一样 (对齐)
            Traverse(children[i], branchCol, nextY, resultList, colTracker);
        }
    }

    private int GetRightmostColumn(List<GraphNodeViewModel> nodes)
    {
        if (nodes.Count == 0) return 0;
        double maxX = nodes.Max(n => n.X);
        return (int)((maxX - PaddingX) / (ColWidth + ColGap));
    }
}