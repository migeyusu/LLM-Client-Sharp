using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;

namespace LLMClient.Dialog.Controls;

public class DialogGraphViewModel : BaseViewModel
{
    private readonly IDialogGraphViewModel _dialogGraph;
    
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
        get => SelectedLeaf != null && _dialogGraph.IsNodeSelectable(SelectedLeaf.Data);
    }

    public ICommand SelectCommand { get; }

    public DialogGraphViewModel(IDialogGraphViewModel viewModel, IDialogItem? rootNode = null)
    {
        _dialogGraph = viewModel;
        LoadTree(rootNode ?? viewModel.RootNode);
        SelectCommand = new RelayCommand(() =>
        {
            if (SelectedLeaf == null || !_dialogGraph.IsNodeSelectable(SelectedLeaf.Data)) return;
            viewModel.CurrentLeaf = SelectedLeaf.Data;
            MessageEventBus.Publish("您已切换到所选节点。");
        });
    }

    public void DeleteRequestItem(IRequestItem item)
    {
        _dialogGraph.DeleteItem(item);
    }

    // 当树变化时调用此方法
    public void LoadTree(IDialogItem root)
    {
        FlatNodes.Clear();
        FlattenRecursive(root);
        SelectedLeaf = FlatNodes.FirstOrDefault(model => model.Data == _dialogGraph.CurrentLeaf);
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