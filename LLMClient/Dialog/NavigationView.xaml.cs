using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace LLMClient.Dialog;

public partial class NavigationView : UserControl
{
    public NavigationView()
    {
        InitializeComponent();
    }

    private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is INavigationViewModel vm && e.NewValue is IDialogItem node)
        {
            if (!vm.IsNodeSelectable(node))
            {
                // 禁止选择
                return;
            }

            vm.CurrentLeaf = node;
        }
    }
}