using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Windows.Data;

namespace LLMClient.UI.Component;

public class MarkdownToDocumentConverter : IValueConverter
{
    public static MarkdownToDocumentConverter Instance { get; } = new MarkdownToDocumentConverter();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return str.ToFlowDocument();
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}