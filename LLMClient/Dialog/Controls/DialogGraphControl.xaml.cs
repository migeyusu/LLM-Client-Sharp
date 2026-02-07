using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;

namespace LLMClient.Dialog.Controls;

public class DialogGraphViewModel : BaseViewModel
{
    private readonly INavigationViewModel _viewModel;
    private GraphNodeViewModel? _selectedLeaf;

    public GraphNodeViewModel? SelectedLeaf
    {
        get => _selectedLeaf;
        set
        {
            if (Equals(value, _selectedLeaf)) return;
            _selectedLeaf = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanSelect));
        }
    }

    public ObservableCollection<GraphNodeViewModel> FlatNodes { get; } = new();

    public bool CanSelect
    {
        get => SelectedLeaf != null && _viewModel.IsNodeSelectable(SelectedLeaf.Data);
    }

    public ICommand SelectCommand { get; }

    public DialogGraphViewModel(INavigationViewModel viewModel, IDialogItem? rootNode = null)
    {
        _viewModel = viewModel;
        LoadTree(rootNode ?? viewModel.RootNode);
        SelectCommand = new RelayCommand(() =>
        {
            if (SelectedLeaf == null || !_viewModel.IsNodeSelectable(SelectedLeaf.Data)) return;
            viewModel.CurrentLeaf = SelectedLeaf.Data;
            MessageEventBus.Publish("您已切换到所选节点。");
        });
    }

    // 当树变化时调用此方法
    public void LoadTree(IDialogItem root)
    {
        FlatNodes.Clear();
        FlattenRecursive(root);
        SelectedLeaf = FlatNodes.FirstOrDefault(model => model.Data == _viewModel.CurrentLeaf);
    }

    private void FlattenRecursive(IDialogItem? item)
    {
        if (item == null)
        {
            return;
        }

        // 创建 VM，只负责显示逻辑(如 Fork 标记)，不再负责坐标！
        var nodeVm = new GraphNodeViewModel(item);
        FlatNodes.Add(nodeVm);
        foreach (var child in item.Children)
        {
            FlattenRecursive(child);
        }
    }
}

public partial class DialogGraphControl : UserControl
{
    public DialogGraphControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        this.Width = double.NaN;
        this.Height = double.NaN;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // 锁定当前大小，防止后续因内容缩放导致控件尺寸跳变
        if (ActualWidth > 0 && ActualHeight > 0)
        {
            Width = ActualWidth;
            Height = ActualHeight;
        }
    }

    private void ZoomIn_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        ZoomSlider.Value = Math.Min(ZoomSlider.Maximum, ZoomSlider.Value + 0.2);
    }

    private void ZoomOut_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        ZoomSlider.Value = Math.Max(ZoomSlider.Minimum, ZoomSlider.Value - 0.2);
    }

    private void ZoomReset_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        ZoomSlider.Value = 1.0;
    }
}