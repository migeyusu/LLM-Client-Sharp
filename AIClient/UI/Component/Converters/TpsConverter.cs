using System.Globalization;
using System.Windows.Data;
using LLMClient.Abstraction;

namespace LLMClient.UI.Component.Converters;

public class TpsConverter : IValueConverter
{
    public static TpsConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IResponse response)
        {
            if (response.Tokens == 0)
            {
                return 0;
            }

            return response.Tokens / (response.Duration - (response.Latency) / 1000f);
        }

        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}