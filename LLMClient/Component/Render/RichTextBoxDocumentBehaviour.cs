using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.Xaml.Behaviors;

namespace LLMClient.Component.Render;

public class RichTextBoxDocumentBehaviour : Behavior<RichTextBox>
{
    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.Register(nameof(Document), typeof(System.Windows.Documents.FlowDocument),
            typeof(RichTextBoxDocumentBehaviour),
            new PropertyMetadata(null, OnDocumentChanged));

    public FlowDocument Document
    {
        get => (FlowDocument)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    private static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RichTextBoxDocumentBehaviour behavior && behavior.AssociatedObject != null)
        {
            if (e.NewValue is FlowDocument newDocument)
            {
                if (newDocument.Parent is RichTextBox oldOwner && !ReferenceEquals(oldOwner, behavior.AssociatedObject))
                {
                    oldOwner.Document = new FlowDocument();
                }
                behavior.AssociatedObject.Document = newDocument;
            }
        }
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (Document?.Parent is RichTextBox richTextBox && !ReferenceEquals(richTextBox, AssociatedObject))
        {
            richTextBox.Document = new FlowDocument();
        }

        AssociatedObject.Document = Document ?? new FlowDocument();
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.Document = new FlowDocument();
    }
}