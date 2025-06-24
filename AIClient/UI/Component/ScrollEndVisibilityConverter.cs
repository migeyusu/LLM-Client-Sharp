using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LLMClient.UI.Component;

/// <summary>
/// 根据滚动位置决定箭头可见性：
/// IsLeft=true  → 仅在最左端隐藏；
/// IsLeft=false → 仅在最右端隐藏。
/// </summary>
public sealed class ScrollEndVisibilityConverter : IMultiValueConverter
{
    public static readonly ScrollEndVisibilityConverter LeftEnd =
        new ScrollEndVisibilityConverter() { IsLeft = true };

    public static readonly ScrollEndVisibilityConverter
        RightEnd = new ScrollEndVisibilityConverter() { IsLeft = false };

    public bool IsLeft { get; set; } // 左右侧标记

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 ||
            !(values[0] is double offset) ||
            !(values[1] is double scrollable))
            return Visibility.Visible;

        bool hide = IsLeft
            ? offset <= 0
            : offset >= scrollable;

        return hide ? Visibility.Collapsed : Visibility.Visible;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}