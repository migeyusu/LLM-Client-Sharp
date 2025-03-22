using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LLMClient.UI.Component;

public class AutoScrollIntoView
{
    public static readonly DependencyProperty ScrollToItemProperty = DependencyProperty.RegisterAttached(
        "ScrollToItem", typeof(object), typeof(AutoScrollIntoView),
        new FrameworkPropertyMetadata(default(object),
            new PropertyChangedCallback(ScrollToItemPropertyChangedCallback)));

    private static void ScrollToItemPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListBox listBox)
        {
            try
            {
                listBox.ScrollIntoView(e.NewValue);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("What the..." + ex.Message);
            }
        }
    }

    public static void SetScrollToItem(ItemsControl element, object value)
    {
        element.SetValue(ScrollToItemProperty, value);
    }

    public static object GetScrollToItem(ItemsControl element)
    {
        return (object)element.GetValue(ScrollToItemProperty);
    }
}