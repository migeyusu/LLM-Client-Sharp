using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace LLMClient.UI.Render;

public class RichTextBoxBehaviour : Behavior<RichTextBox>
{
    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.Register(nameof(Document), typeof(System.Windows.Documents.FlowDocument), typeof(RichTextBoxBehaviour), 
            new PropertyMetadata(null, OnDocumentChanged));

    public System.Windows.Documents.FlowDocument Document
    {
        get => (System.Windows.Documents.FlowDocument)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    private static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RichTextBoxBehaviour behavior && behavior.AssociatedObject != null)
        {
            behavior.AssociatedObject.Document = (System.Windows.Documents.FlowDocument)e.NewValue;
        }
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Document = Document;
    }
}