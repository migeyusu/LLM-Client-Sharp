using System.Globalization;
using System.Windows.Data;

namespace LLMClient.UI.Component;

public class KUnitConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value?.ToString();
        if (string.IsNullOrEmpty(s))
        {
            return 0;
        }

        if (Int32.TryParse(s, out int d))
        {
            return d / 1000;
        }

        return 0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value?.ToString();
        if (string.IsNullOrEmpty(s))
        {
            return 0;
        }

        if (Int32.TryParse(s, out int d))
        {
            return d * 1000;
        }

        return 0;
    }
}