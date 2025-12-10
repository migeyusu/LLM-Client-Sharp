using System.Globalization;
using System.Windows.Data;
using LLMClient.Data;
using LLMClient.Endpoints;

namespace LLMClient.Component.Converters;

public class EnumToIconConverter : IValueConverter
{
    public static EnumToIconConverter Instance;

    static EnumToIconConverter()
    {
        Instance = new EnumToIconConverter();
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ModelIconType iconType)
        {
            return ImageExtensions.GetIcon(iconType).CurrentSource;
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}