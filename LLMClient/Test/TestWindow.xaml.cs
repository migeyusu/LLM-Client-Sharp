using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using CommunityToolkit.Mvvm.ComponentModel;
using Elsa.Mediator.Contracts;
using LLMClient.Component.Render;
using LLMClient.Component.ViewModel.Base;
using Path = System.Windows.Shapes.Path;

namespace LLMClient.Test;

public partial class TestWindow : Window, INotifyPropertyChanged
{
  

    public TestWindow()
    {
        this.InitializeComponent();
        LoadDemoData();
        
    }
    
     private void LoadDemoData()
    {
        // 构建演示数据：模拟一个有分叉的对话
        var root = new ChatNode
        {
            Role = "user",
            Content = "帮我写一个排序算法",
            Children = new ObservableCollection<ChatNode>
            {
                new ChatNode
                {
                    Role = "assistant",
                    Content = "好的，我可以为您介绍几种常见的排序算法。请问您想要哪种？",
                    Children = new ObservableCollection<ChatNode>
                    {
                        // 分支 1：用户选择快速排序
                        new ChatNode
                        {
                            Role = "user",
                            Content = "快速排序",
                            Children = new ObservableCollection<ChatNode>
                            {
                                new ChatNode
                                {
                                    Role = "assistant",
                                    Content = "快速排序是一种分治算法，平均时间复杂度 O(n log n)...",
                                    Children = new ObservableCollection<ChatNode>
                                    {
                                        new ChatNode
                                        {
                                            Role = "user",
                                            Content = "请给我 C# 代码实现"
                                        }
                                    }
                                }
                            }
                        },
                        // 分支 2：用户选择归并排序
                        new ChatNode
                        {
                            Role = "user",
                            Content = "归并排序",
                            Children = new ObservableCollection<ChatNode>
                            {
                                new ChatNode
                                {
                                    Role = "assistant",
                                    Content = "归并排序也是分治算法，稳定且时间复杂度为 O(n log n)..."
                                }
                            }
                        },
                        // 分支 3：用户问其他问题
                        new ChatNode
                        {
                            Role = "user",
                            Content = "什么是时间复杂度？",
                            Children = new ObservableCollection<ChatNode>
                            {
                                new ChatNode
                                {
                                    Role = "assistant",
                                    Content = "时间复杂度是衡量算法运行时间随输入规模增长的度量...",
                                    Children = new ObservableCollection<ChatNode>
                                    {
                                        new ChatNode
                                        {
                                            Role = "user",
                                            Content = "能举个例子吗？"
                                        },
                                        new ChatNode
                                        {
                                            Role = "user",
                                            Content = "空间复杂度呢？"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        BranchTree.RootNodes = new ObservableCollection<ChatNode> { root };
    }
    
    
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
public class ChatNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Role { get; set; } = "user";  // user / assistant
    public string Content { get; set; } = "";
    public ObservableCollection<ChatNode> Children { get; set; } = new();

    // 布局计算用（运行时填充）
    public double X { get; set; }
    public double Y { get; set; }
    public int Column { get; set; }  // 所在列（分支索引）
    
    public string DisplayText => Content.Length > 40 
        ? Content[..40] + "…" 
        : Content;
}
public class BranchTreeControl : Canvas
{
    public static readonly DependencyProperty RootNodesProperty =
        DependencyProperty.Register(
            nameof(RootNodes),
            typeof(IEnumerable<ChatNode>),
            typeof(BranchTreeControl),
            new PropertyMetadata(null, OnRootNodesChanged));

    public IEnumerable<ChatNode> RootNodes
    {
        get => (IEnumerable<ChatNode>)GetValue(RootNodesProperty);
        set => SetValue(RootNodesProperty, value);
    }

    // 布局常量
    private const double NodeRadius = 8;
    private const double RowHeight = 60;
    private const double ColWidth = 200;
    private const double LeftPadding = 30;
    private const double TopPadding = 20;

    private static void OnRootNodesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BranchTreeControl control)
            control.Render();
    }

    private void Render()
    {
        Children.Clear();
        if (RootNodes == null) return;

        int currentRow = 0;
        var columnUsage = new Dictionary<int, int>(); // column -> 最大使用行

        foreach (var root in RootNodes)
        {
            LayoutNode(root, ref currentRow, 0, columnUsage);
        }

        // 绘制
        foreach (var root in RootNodes)
        {
            DrawNode(root);
        }
    }

    /// <summary>
    /// 递归计算节点位置
    /// </summary>
    private void LayoutNode(ChatNode node, ref int row, int column, Dictionary<int, int> columnUsage)
    {
        node.Column = column;
        node.X = LeftPadding + column * ColWidth;
        node.Y = TopPadding + row * RowHeight;

        columnUsage[column] = row;
        row++;

        for (int i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            int childColumn = (i == 0) ? column : FindAvailableColumn(columnUsage, row);
            LayoutNode(child, ref row, childColumn, columnUsage);
        }
    }

    private int FindAvailableColumn(Dictionary<int, int> columnUsage, int currentRow)
    {
        // 寻找一个不会重叠的列
        for (int col = 0; col < 20; col++)
        {
            if (!columnUsage.ContainsKey(col) || columnUsage[col] < currentRow - 1)
                return col;
        }
        return columnUsage.Count;
    }

    /// <summary>
    /// 递归绘制节点和连线
    /// </summary>
    private void DrawNode(ChatNode node)
    {
        // 1. 绘制到子节点的连线
        foreach (var child in node.Children)
        {
            DrawConnection(node, child);
            DrawNode(child);
        }

        // 2. 绘制节点圆圈
        var circle = new Ellipse
        {
            Width = NodeRadius * 2,
            Height = NodeRadius * 2,
            Fill = GetRoleBrush(node.Role),
            Stroke = Brushes.White,
            StrokeThickness = 2
        };
        SetLeft(circle, node.X - NodeRadius);
        SetTop(circle, node.Y - NodeRadius);
        Children.Add(circle);

        // 3. 绘制文本标签
        var label = new Border
        {
            Background = GetRoleBrush(node.Role, 0.15),
            BorderBrush = GetRoleBrush(node.Role, 0.4),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4, 8, 4),
            Child = new TextBlock
            {
                Text = node.DisplayText,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                MaxWidth = 160,
                TextTrimming = TextTrimming.CharacterEllipsis
            }
        };
        SetLeft(label, node.X + NodeRadius + 10);
        SetTop(label, node.Y - 12);
        Children.Add(label);

        // 4. 角色标记
        var roleTag = new TextBlock
        {
            Text = node.Role == "user" ? "U" : "A",
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White
        };
        SetLeft(roleTag, node.X - 4);
        SetTop(roleTag, node.Y - 6);
        Children.Add(roleTag);
    }

    /// <summary>
    /// 绘制 Git 风格连接线
    /// </summary>
    private void DrawConnection(ChatNode parent, ChatNode child)
    {
        var brush = GetRoleBrush(parent.Role, 0.6);

        if (parent.Column == child.Column)
        {
            // 同列：直线
            var line = new Line
            {
                X1 = parent.X,
                Y1 = parent.Y + NodeRadius,
                X2 = child.X,
                Y2 = child.Y - NodeRadius,
                Stroke = brush,
                StrokeThickness = 2
            };
            Children.Add(line);
        }
        else
        {
            // 不同列：折线（先斜后直）
            double midY = parent.Y + RowHeight * 0.6;

            var path = new Path
            {
                Stroke = brush,
                StrokeThickness = 2,
                Data = Geometry.Parse(
                    $"M {parent.X},{parent.Y + NodeRadius} " +
                    $"C {parent.X},{midY} {child.X},{midY} {child.X},{midY} " +
                    $"L {child.X},{child.Y - NodeRadius}")
            };
            Children.Add(path);
        }
    }

    private static SolidColorBrush GetRoleBrush(string role, double opacity = 1.0)
    {
        var color = role == "user"
            ? Color.FromRgb(59, 130, 246)   // 蓝色
            : Color.FromRgb(16, 185, 129);  // 绿色

        return new SolidColorBrush(Color.FromArgb(
            (byte)(255 * opacity), color.R, color.G, color.B));
    }
}
