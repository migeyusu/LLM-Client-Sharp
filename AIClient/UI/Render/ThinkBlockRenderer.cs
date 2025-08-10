using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;

namespace LLMClient.UI.Render;

public class ThinkBlockRenderer : WpfObjectRenderer<ThinkBlock>
{
    public static ComponentResourceKey ThinkBlockExpanderStyleKey { get; } =
        new(typeof(ThinkBlockRenderer), (object)nameof(ThinkBlockExpanderStyleKey));

    protected override void Write(WpfRenderer renderer, ThinkBlock obj)
    {
        var blockUiContainer = new BlockUIContainer();
        renderer.Push(blockUiContainer);
        var expander = new Expander()
        {
            IsExpanded = false,
        };
        expander.SetResourceReference(FrameworkElement.StyleProperty, ThinkBlockExpanderStyleKey);
        renderer.Push(expander);
        expander.Content = obj.Lines.ToString().Trim();
        renderer.Pop();
        renderer.Pop();
    }
}