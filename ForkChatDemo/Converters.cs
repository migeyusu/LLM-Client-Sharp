using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ForkChatDemo.Models;

namespace ForkChatDemo.Converters;

/// <summary>
/// 角色 -> 背景色
/// </summary>
public class RoleToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            ChatRole.User => new SolidColorBrush(Color.FromRgb(220, 240, 255)),      // 浅蓝
            ChatRole.Assistant => new SolidColorBrush(Color.FromRgb(240, 255, 240)), // 浅绿
            ChatRole.System => new SolidColorBrush(Color.FromRgb(255, 250, 230)),    // 浅黄
            _ => Brushes.White
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 角色 -> 边框色
/// </summary>
public class RoleToBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            ChatRole.User => new SolidColorBrush(Color.FromRgb(100, 160, 220)),
            ChatRole.Assistant => new SolidColorBrush(Color.FromRgb(100, 180, 100)),
            ChatRole.System => new SolidColorBrush(Color.FromRgb(200, 180, 100)),
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 角色 -> 标签前景色
/// </summary>
public class RoleToForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            ChatRole.User => new SolidColorBrush(Color.FromRgb(30, 80, 140)),
            ChatRole.Assistant => new SolidColorBrush(Color.FromRgb(30, 120, 50)),
            ChatRole.System => new SolidColorBrush(Color.FromRgb(140, 120, 40)),
            _ => Brushes.Black
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 布尔 -> 可见性
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 子节点数量 -> 分叉标记可见性
/// </summary>
public class ChildCountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int count && count > 1 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
/// <summary>
/// 兄弟索引 -> 分支颜色
/// </summary>
public class SiblingIndexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            0 => new SolidColorBrush(Color.FromRgb(74, 144, 217)),   // 蓝色
            1 => new SolidColorBrush(Color.FromRgb(102, 187, 106)),  // 绿色
            2 => new SolidColorBrush(Color.FromRgb(255, 167, 38)),   // 橙色
            _ => new SolidColorBrush(Color.FromRgb(149, 117, 205))   // 紫色（更多分支）
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
