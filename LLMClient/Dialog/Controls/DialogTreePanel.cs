using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LLMClient.Dialog.Controls;

public class DialogTreePanel : Panel
{
    // --- 配置参数 ---
    public double ColumnGap { get; set; } = 80;
    public double RowGap { get; set; } = 60;

    // 节点的期望宽度，须与 XAML ItemTemplate 宽度匹配
    public static readonly DependencyProperty ItemWidthProperty =
        DependencyProperty.Register(nameof(ItemWidth), typeof(double), typeof(DialogTreePanel),
            new FrameworkPropertyMetadata(260.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    // --- 内部缓存 ---
    // 保存计算好位置的矩形
    private readonly Dictionary<UIElement, Rect> _layoutCache = new();

    // 保存需要绘制的连线信息: (起点, 终点, 是否主分支)
    private readonly List<(Point Start, Point End, bool IsMain)> _connections = new();

    // 画笔缓存
    private readonly Pen _mainPen;
    private readonly Pen _forkPen;

    public DialogTreePanel()
    {
        var mainColor = (Color)ColorConverter.ConvertFromString("#cbd5e1"); // 灰色
        var forkColor = (Color)ColorConverter.ConvertFromString("#f59e0b"); // 橙色

        var mainBrush = new SolidColorBrush(mainColor);
        var forkBrush = new SolidColorBrush(forkColor);
        mainBrush.Freeze();
        forkBrush.Freeze();

        _mainPen = new Pen(mainBrush, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        _forkPen = new Pen(forkBrush, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        _mainPen.Freeze();
        _forkPen.Freeze();
    }

    // 第一步：测量并计算位置
    protected override Size MeasureOverride(Size availableSize)
    {
        _layoutCache.Clear();
        _connections.Clear();

        if (InternalChildren.Count == 0) return new Size(0, 0);

        // 1. 让所有子元素先自我测量，获取真实高度
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(ItemWidth, double.PositiveInfinity));
        }

        // 2. 构建图结构映射
        var elementMap = new Dictionary<Guid, UIElement>();
        var childrenMap = new Dictionary<Guid, List<Guid>>();
        Guid? rootId = null;

        foreach (UIElement child in InternalChildren)
        {
            var id = GraphProps.GetNodeId(child);
            var parentId = GraphProps.GetParentId(child);

            elementMap[id] = child;
            if (parentId == null || parentId == Guid.Empty)
            {
                rootId = id;
            }
            else
            {
                if (!childrenMap.ContainsKey(parentId.Value))
                    childrenMap[parentId.Value] = new List<Guid>();
                childrenMap[parentId.Value].Add(id);
            }
        }

        if (rootId == null)
        {
            //当在里面找不到前继节点时就是根节点
            foreach (var id in childrenMap.Keys)
            {
                if (!elementMap.ContainsKey(id))
                {
                    rootId = childrenMap[id].FirstOrDefault();
                    break;
                }
            }
        }

        if (rootId == null && InternalChildren.Count > 0)
            return new Size(0, 0); // 没找到根，无法布局

        // 3. 执行布局逻辑 (DFS)
        var columnBottoms = new Dictionary<int, double>(); // 记录每列底部 Y 值
        double maxX = 0, maxY = 0;

        // 递归布局函数
        void LayoutNode(Guid currentId, int colIndex)
        {
            if (!elementMap.TryGetValue(currentId, out var element)) return;

            double w = ItemWidth;
            double h = element.DesiredSize.Height; // 【关键】使用测量后的真实高度

            // 计算 X
            double x = 20 + colIndex * (w + ColumnGap);

            // 计算 Y
            double startY = 20;
            if (columnBottoms.TryGetValue(colIndex, out var currentY))
            {
                startY = currentY;
            }

            // 规则：不能高于父节点底部 (视觉上的自上而下)
            var pId = GraphProps.GetParentId(element);
            if (pId != null && elementMap.TryGetValue(pId.Value, out var parentElem) &&
                _layoutCache.TryGetValue(parentElem, out var pRect))
            {
                startY = Math.Max(startY, pRect.Bottom + RowGap);
            }

            var rect = new Rect(x, startY, w, h);
            _layoutCache[element] = rect;

            maxX = Math.Max(maxX, rect.Right);
            maxY = Math.Max(maxY, rect.Bottom);
            columnBottoms[colIndex] = rect.Bottom + RowGap; // 预留空隙

            // 处理子节点
            if (childrenMap.TryGetValue(currentId, out var children))
            {
                var startPoint = new Point(rect.Left + rect.Width / 2, rect.Bottom);

                for (int i = 0; i < children.Count; i++)
                {
                    // 只有第一个孩子继承当前列 (主分支)，其他孩子去新列
                    int targetCol = (i == 0)
                        ? colIndex
                        : FindNextColumn(colIndex + 1, columnBottoms, rect.Bottom + RowGap);

                    LayoutNode(children[i], targetCol);

                    // 记录连线 (现在子节点位置已算出)
                    if (elementMap.TryGetValue(children[i], out var childElem) &&
                        _layoutCache.TryGetValue(childElem, out var cRect))
                    {
                        var endPoint = new Point(cRect.Left + cRect.Width / 2, cRect.Top);
                        _connections.Add((startPoint, endPoint, i == 0));
                    }
                }
            }
        }

        if (rootId != null) LayoutNode(rootId.Value, 0);

        return new Size(maxX + 20, maxY + 20);
    }

    private int FindNextColumn(int startCol, Dictionary<int, double> map, double minHeight)
    {
        int col = startCol;
        while (true)
        {
            // 如果该列没东西，或者该列最下方远高于我们需要的位置，就可以用
            if (!map.ContainsKey(col) || map[col] < minHeight + 10) return col;
            col++;
        }
    }

    // 第二步：实际放置元素
    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (UIElement child in InternalChildren)
        {
            if (_layoutCache.TryGetValue(child, out var rect))
            {
                child.Arrange(rect);
            }
            else
            {
                // 没在树里的节点（如孤儿节点）隐藏起来
                child.Arrange(new Rect(0, 0, 0, 0));
            }
        }

        return finalSize;
    }

    // 第三步：高效绘制连线
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        foreach (var conn in _connections)
        {
            var pen = conn.IsMain ? _mainPen : _forkPen;
            var start = conn.Start;
            var end = conn.End;

            // 贝塞尔曲线控制点计算
            double cp1Y = start.Y + RowGap / 2;
            double cp2Y = end.Y - RowGap / 2;

            var p1 = new Point(start.X, cp1Y);
            // 贝塞尔的第二个控制点 X 坐标与终点对齐，制造垂直切入效果
            var p2 = new Point(end.X, cp2Y);

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(start, false, false);
                ctx.BezierTo(p1, p2, end, true, false);
            }

            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);
        }
    }
}