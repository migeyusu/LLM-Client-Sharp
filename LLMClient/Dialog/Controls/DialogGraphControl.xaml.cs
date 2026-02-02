using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using LLMClient.Component.ViewModel.Base;

namespace LLMClient.Dialog.Controls;

public class DialogGraphViewModel : BaseViewModel
{
    public ObservableCollection<GraphNodeViewModel> FlatNodes { get; } = new();

    public DialogGraphViewModel(INavigationViewModel viewModel)
    {
        LoadTree(viewModel.RootNode);
    }

    // 当树变化时调用此方法
    public void LoadTree(IDialogItem root)
    {
        FlatNodes.Clear();
        FlattenRecursive(root);
    }

    private void FlattenRecursive(IDialogItem item)
    {
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