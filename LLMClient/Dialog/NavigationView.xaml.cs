using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using LambdaConverters;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog;

public interface INavigationViewModel
{
    IDialogItem RootNode { get; }

    IDialogItem CurrentLeaf { get; set; }

    bool IsNodeSelectable(IDialogItem item);
}

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

internal static class NavigationConverters
{
    public static readonly IValueConverter RoleToBackground = ValueConverter.Create<ChatRole, Brush>(e =>
    {
        if (e.Value == ChatRole.User)
        {
            return new SolidColorBrush(Color.FromRgb(220, 240, 255)); // 浅蓝
        }

        if (e.Value == ChatRole.Assistant)
        {
            return new SolidColorBrush(Color.FromRgb(240, 255, 240)); // 浅绿
        }

        if (e.Value == ChatRole.System)
        {
            return new SolidColorBrush(Color.FromRgb(255, 250, 230)); // 浅黄
        }

        return Brushes.White;
    });

    public static readonly IValueConverter RoleToBorder = ValueConverter.Create<ChatRole, Brush>(e =>
    {
        if (e.Value == ChatRole.User)
        {
            return new SolidColorBrush(Color.FromRgb(100, 160, 220));
        }
        else if (e.Value == ChatRole.Assistant)
        {
            return new SolidColorBrush(Color.FromRgb(100, 180, 100));
        }
        else if (e.Value == ChatRole.System)
        {
            return new SolidColorBrush(Color.FromRgb(200, 180, 100));
        }

        return Brushes.Gray;
    });

    public static readonly IValueConverter RoleToForeground = ValueConverter.Create<ChatRole, Brush>(e =>
    {
        if (e.Value == ChatRole.User)
        {
            return new SolidColorBrush(Color.FromRgb(30, 80, 140));
        }
        else if (e.Value == ChatRole.Assistant)
        {
            return new SolidColorBrush(Color.FromRgb(30, 120, 50));
        }
        else if (e.Value == ChatRole.System)
        {
            return new SolidColorBrush(Color.FromRgb(140, 120, 40));
        }

        return Brushes.Black;
    });

    public static readonly IValueConverter SiblingIndexToBrush = ValueConverter.Create<int, Brush>(e => e.Value switch
    {
        0 => new SolidColorBrush(Color.FromRgb(74, 144, 217)), // 蓝色
        1 => new SolidColorBrush(Color.FromRgb(102, 187, 106)), // 绿色
        2 => new SolidColorBrush(Color.FromRgb(255, 167, 38)), // 橙色
        _ => new SolidColorBrush(Color.FromRgb(149, 117, 205)) // 紫色（更多分支）
    });
}