using System.Windows;
using System.Windows.Controls;
using LLMClient.Abstraction;

namespace LLMClient.Dialog;

public partial class ModelSelectionView : UserControl
{
    public ModelSelectionView()
    {
        InitializeComponent();
    }

    ModelSelectionPopupViewModel ViewModel
    {
        get { return (DataContext as ModelSelectionPopupViewModel)!; }
    }

    private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        var newValue = e.NewValue;
        if (newValue is ILLMModel modelInfo)
        {
            ViewModel.SelectedModel = modelInfo;
        }
    }
}