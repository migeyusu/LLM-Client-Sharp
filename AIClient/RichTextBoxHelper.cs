using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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