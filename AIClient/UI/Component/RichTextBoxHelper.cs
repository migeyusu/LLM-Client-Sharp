using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace LLMClient.UI.Component;

public class RichTextBoxHelper : DependencyObject
{
    public static FlowDocument GetDocumentXaml(DependencyObject obj)
    {
        return (FlowDocument)obj.GetValue(DocumentXamlProperty);
    }

    public static void SetDocumentXaml(DependencyObject obj, FlowDocument value)
    {
        obj.SetValue(DocumentXamlProperty, value);
    }

    public static readonly DependencyProperty DocumentXamlProperty =
        DependencyProperty.RegisterAttached(
            "DocumentXaml",
            typeof(FlowDocument),
            typeof(RichTextBoxHelper),
            new FrameworkPropertyMetadata
            {
                BindsTwoWayByDefault = true,
                PropertyChangedCallback = (obj, e) =>
                {
                    var richTextBox = (RichTextBox)obj;
                    var xaml = GetDocumentXaml(richTextBox);
                    if (xaml != null)
                    {
                        richTextBox.Document = xaml;
                    }
                    else
                    {
                        richTextBox.Document = new FlowDocument();
                    }
                }
            });
}