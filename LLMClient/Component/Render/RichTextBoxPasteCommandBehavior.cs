using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace LLMClient.Component.Render;

public class RichTextBoxPasteCommandBehavior : Behavior<RichTextBox>
{
    public static readonly DependencyProperty PasteCommandProperty =
        DependencyProperty.Register(nameof(PasteCommand), typeof(ICommand), typeof(RichTextBoxPasteCommandBehavior),
            new PropertyMetadata(null));

    public ICommand PasteCommand
    {
        get => (ICommand)GetValue(PasteCommandProperty);
        set => SetValue(PasteCommandProperty, value);
    }
    
    protected override void OnAttached()
    {
        base.OnAttached();
        CommandManager.AddPreviewExecutedHandler(AssociatedObject, OnPreviewPaste);
    }

    protected override void OnDetaching()
    {
        CommandManager.RemovePreviewExecutedHandler(AssociatedObject, OnPreviewPaste);
        base.OnDetaching();
    }

    private void OnPreviewPaste(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command != ApplicationCommands.Paste) return;
        PasteCommand?.Execute(e);
    }
    
}