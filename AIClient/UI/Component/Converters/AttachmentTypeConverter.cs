using System.Globalization;
using System.Windows.Data;
using LLMClient.UI.Dialog;
using MaterialDesignThemes.Wpf;

namespace LLMClient.UI.Component.Converters;

public class AttachmentTypeConverter : IValueConverter
{
    public static readonly AttachmentTypeConverter Instance = new AttachmentTypeConverter();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AttachmentType type)
        {
            switch (type)
            {
                case AttachmentType.Image:
                    return PackIconKind.Image;
                case AttachmentType.File:
                    return PackIconKind.File;
                case AttachmentType.Audio:
                    return PackIconKind.Audio;
                case AttachmentType.Video:
                    return PackIconKind.Video;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return PackIconKind.None;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}