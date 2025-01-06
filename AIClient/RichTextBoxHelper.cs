using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace LLMClient;

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
                    richTextBox.Document = xaml;
                }
            });
}