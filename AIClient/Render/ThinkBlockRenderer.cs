using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;

namespace LLMClient.Render;

public class ThinkBlockRenderer : WpfObjectRenderer<ThinkBlock>
{
    public static ComponentResourceKey ThinkBlockExpanderStyleKey { get; } =
        new ComponentResourceKey(typeof(ThinkBlockRenderer), (object)nameof(ThinkBlockExpanderStyleKey));

    protected override void Write(WpfRenderer renderer, ThinkBlock obj)
    {
        var blockUiContainer = new BlockUIContainer();
        renderer.Push(blockUiContainer);
        var expander = new Expander()
        {
            IsExpanded = false,
            Margin = new Thickness(0, 5, 0, 5),
        };
        expander.SetResourceReference(FrameworkElement.StyleProperty, ThinkBlockExpanderStyleKey);
        renderer.Push(expander);
        var content = obj.Lines.ToString();
        expander.Content = content;
        expander.Header = content;
        renderer.Pop();
        renderer.Pop();
    }
}