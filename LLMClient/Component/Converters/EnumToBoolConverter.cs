using System.Globalization;
using System.Windows.Data;

namespace LLMClient.Component.Converters;

public class EnumToBoolConverter : IValueConverter
{
    // 可选：允许通过参数指定默认Enum值，如果未指定则用targetType的默认
    private static readonly object DefaultValue = Binding.DoNothing;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Enum enumValue && parameter is Enum targetEnum)
        {
            // 简单相等检查
            return enumValue.Equals(targetEnum);
        }
        return false; // 默认不选中
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && parameter is Enum targetEnum && targetType.IsEnum)
        {
            if (isChecked)
            {
                // 选中：返回指定的Enum值
                return targetEnum;
            }
            else
            {
                // 未选中：返回默认Enum值（这里假设None=0，你可以改为其他常量）
                // 示例：返回 Enum.ToObject(targetType, 0);  // 假设0是None
                var defaultEnum = Enum.ToObject(targetType, 0);
                return defaultEnum;
            }
        }
        return DefaultValue; // 不改变原值
    }
}