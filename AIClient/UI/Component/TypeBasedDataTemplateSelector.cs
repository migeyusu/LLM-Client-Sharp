using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace LLMClient.UI.Component;

[ContentProperty("Template")]
public class DataTemplateTypePair
{
    private DataTemplate? _template;

    public Type? TargetType { get; private set; }

    public DataTemplate? Template
    {
        get => _template;
        set
        {
            _template = value;
            if (value != null)
            {
                this.TargetType = (Type?)value.DataType;
            }
        }
    }
}

[ContentProperty("Pairs")]
public class TypeBasedDataTemplateSelector : DataTemplateSelector
{
    public DataTemplate? EmptyTemplate { get; set; }

    private readonly List<DataTemplateTypePair> _templateTypePairs = new List<DataTemplateTypePair>();

    public List<DataTemplateTypePair> Pairs => _templateTypePairs;

    public override DataTemplate SelectTemplate(object? item, DependencyObject container)
    {
        if (item == null)
            return new DataTemplate();
        var itemType = item.GetType();
        foreach (var pair in _templateTypePairs)
        {
            if (pair.TargetType?.IsAssignableFrom(itemType) == true)
            {
                DataTemplate? template;
                if ((template = pair.Template) != null)
                {
                    return template;
                }

                break;
            }
        }

        return EmptyTemplate ?? new DataTemplate();
    }
}