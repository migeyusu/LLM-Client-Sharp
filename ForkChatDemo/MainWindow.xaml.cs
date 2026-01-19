using System.Windows;
using System.Windows.Controls;
using ForkChatDemo.Models;
using ForkChatDemo.ViewModels;

namespace ForkChatDemo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel vm && e.NewValue is ChatNode node)
        {
            vm.SelectedNode = node;
        }
    }
}