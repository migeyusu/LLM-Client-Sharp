using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace LLMClient.Component.Utility
{
    public class RichTextBoxHelper
    {
        public static readonly DependencyProperty DocumentProperty =
            DependencyProperty.RegisterAttached(
                "Document",
                typeof(FlowDocument),
                typeof(RichTextBoxHelper),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDocumentChanged));

        public static FlowDocument GetDocument(DependencyObject obj)
        {
            return (FlowDocument)obj.GetValue(DocumentProperty);
        }

        public static void SetDocument(DependencyObject obj, FlowDocument value)
        {
            obj.SetValue(DocumentProperty, value);
        }

        private static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RichTextBox richTextBox)
            {
                var newDocument = e.NewValue as FlowDocument;
                // Avoid re-setting if it's the same instance to prevent loop if we were observing changes
                if (richTextBox.Document == newDocument)
                    return;

                richTextBox.Document = newDocument ?? new FlowDocument();
            }
        }
    }
}

