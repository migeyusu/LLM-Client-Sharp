using System.Windows;
using ICSharpCode.AvalonEdit;
using Microsoft.Xaml.Behaviors;

namespace LLMClient.Component.Render;

public class TextEditorBehavior : Behavior<TextEditor>
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(TextEditorBehavior),
            new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextEditorBehavior behavior && behavior.AssociatedObject != null)
        {
            var newText = (string?)e.NewValue;
            if (behavior.AssociatedObject.Text != newText)
            {
                behavior.AssociatedObject.Text = newText ?? string.Empty;
            }
        }
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject != null)
        {
            AssociatedObject.Text = Text ?? string.Empty;
            AssociatedObject.TextChanged += AssociatedObject_TextChanged;
        }
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        if (AssociatedObject != null)
        {
            AssociatedObject.TextChanged -= AssociatedObject_TextChanged;
        }
    }

    private void AssociatedObject_TextChanged(object? sender, EventArgs e)
    {
        if (AssociatedObject != null)
        {
            if (Text != AssociatedObject.Text)
            {
                Text = AssociatedObject.Text;
            }
        }
    }
}
