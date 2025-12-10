using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LLMClient.Component.Converters;

public class RoundFloatConverter : IValueConverter
{
    public int Digits { get; set; } = 2;          // 默认保留 2 位

    public object? Convert(object? value, Type t, object? p, CultureInfo c)
    {
        if (value is double d) return d.ToString("F" + Digits, c);
        return value?.ToString();
    }

    public object ConvertBack(object? value, Type t, object? p, CultureInfo c)
    {
        if (double.TryParse(value?.ToString(), NumberStyles.Float, c, out var d))
            return Math.Round(d, Digits);         // 关键：回写时四舍五入
        return DependencyProperty.UnsetValue;     // 让绑定报告异常
    }
}