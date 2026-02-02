using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using LLMClient.Component.ViewModel.Base;

namespace LLMClient.Dialog.Controls;

public class DialogGraphViewModel : BaseViewModel
{
    private readonly GraphLayoutEngine _layoutEngine = new();
    private INavigationViewModel _navigation;
    private double _containerWidth = 2000;
    private double _containerHeight = 2000;

    public DialogGraphViewModel(INavigationViewModel navigation)
    {
        _navigation = navigation;
        RebuildGraph();
    }

    // 绑定源：Node 列表
    public ObservableCollection<GraphNodeViewModel> Nodes { get; } = [];

    // 绑定源：连线列表
    public ObservableCollection<GraphConnectionViewModel> Connections { get; } = [];

    public INavigationViewModel Navigation
    {
        get => _navigation;
        set
        {
            _navigation = value;
            RebuildGraph();
        }
    }

    public double ContainerWidth
    {
        get => _containerWidth;
        set
        {
            if (value.Equals(_containerWidth)) return;
            _containerWidth = value;
            OnPropertyChanged();
        }
    }

    public double ContainerHeight
    {
        get => _containerHeight;
        set
        {
            if (value.Equals(_containerHeight)) return;
            _containerHeight = value;
            OnPropertyChanged();
        }
    }

    // 重绘整个图
    public void RebuildGraph()
    {
        // 调用布局引擎计算并填充 Nodes 和 Connections
        _layoutEngine.Layout(_navigation.RootNode, Nodes, Connections);

        // 动态调整 Canvas 大小
        if (Nodes.Count > 0)
        {
            double maxX = Nodes.Max(n => n.X + n.Width);
            double maxY = Nodes.Max(n => n.Y + n.Height);
            ContainerWidth = Math.Max(ContainerWidth, maxX + 100);
            ContainerHeight = Math.Max(ContainerHeight, maxY + 100);
        }
    }
}

public partial class DialogGraphControl : UserControl
{
    public DialogGraphControl()
    {
        InitializeComponent();
    }
}