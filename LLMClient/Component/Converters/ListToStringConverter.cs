using System.Globalization;
using System.Windows.Data;

namespace LLMClient.Component.Converters;

public class ListToStringConverter : IValueConverter
{
    public char Separator { get; set; } = '\n';

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IEnumerable<string> list)
        {
            return string.Join(Separator.ToString(), list);
        }

        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return str.Split(Separator, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToList();
        }

        return new List<string>();
    }
}