using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using LLMClient.Dialog;

namespace LLMClient.Component.Graph;

public partial class GraphNodeViewModel : ObservableObject
{
    public IDialogItem Data { get; }

    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _width;
    [ObservableProperty] private double _height;
    [ObservableProperty] private bool _hasFork; // 是否显示橙色圆点
    [ObservableProperty] private bool _isMainBranch; // 用于作为主干区分样式

    // 辅助属性：卡片底部中心点，用于连线起始
    public Point BottomCenter => new Point(X + Width / 2, Y + Height);
    
    // 辅助属性：卡片顶部中心点，用于连线结束
    public Point TopCenter => new Point(X + Width / 2, Y);

    public GraphNodeViewModel(IDialogItem data)
    {
        Data = data;
        Width = 260; // 对应 CSS --node-w
        // 高度将在布局计算时基于内容估算
    }
}