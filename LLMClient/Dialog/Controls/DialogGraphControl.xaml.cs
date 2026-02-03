using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Component.ViewModel.Base;
using Microsoft.Xaml.Behaviors.Core;

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

    public DialogGraphViewModel(INavigationViewModel viewModel)
    {
        _viewModel = viewModel;
        LoadTree(viewModel.RootNode);
        SelectedLeaf = FlatNodes.FirstOrDefault(model => model.Data == viewModel.CurrentLeaf);
        SelectCommand = new RelayCommand(() =>
        {
            if (SelectedLeaf == null || _viewModel.IsNodeSelectable(SelectedLeaf.Data)) return;
            viewModel.CurrentLeaf = SelectedLeaf.Data;
        });
    }

    // 当树变化时调用此方法
    public void LoadTree(IDialogItem root)
    {
        FlatNodes.Clear();
        FlattenRecursive(root);
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
    }
}