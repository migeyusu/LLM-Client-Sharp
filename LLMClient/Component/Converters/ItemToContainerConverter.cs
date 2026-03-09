using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace LLMClient.Component.Converters;

public class ItemToContainerConverter : IValueConverter
{
    public static readonly ItemToContainerConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not null && parameter is ItemsControl itemsControl)
        {
            return itemsControl.ItemContainerGenerator.ContainerFromItem(value);
        }

        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
