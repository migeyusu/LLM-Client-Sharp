using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;

namespace LLMClient.Component.UserControls;

public partial class FullModelSelectionView : UserControl
{
    public FullModelSelectionView()
    {
        InitializeComponent();
    }

    private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (sender is not TreeView treeView) return;
        if (treeView.DataContext is not BaseModelSelectionViewModel viewModel) return;
        if (e.NewValue is IEndpointModel modelInfo)
        {
            viewModel.SelectedModel = modelInfo;
        }
    }

    private void TreeView_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TreeView treeView) return;
        if (treeView.DataContext is not BaseModelSelectionViewModel viewModel) return;
        if (treeView.SelectedItem is IEndpointModel modelInfo)
        {
            viewModel.CreateByModelCommand.Execute(modelInfo);
        }
    }
}
