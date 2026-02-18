using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;

namespace LLMClient.Component.Render;

public class RequestBlockRenderer : WpfObjectRenderer<RequestBlock>
{
    public static ComponentResourceKey RequestStyleKey { get; } =
        new(typeof(RequestBlockRenderer), nameof(RequestStyleKey));

    protected override void Write(WpfRenderer renderer, RequestBlock obj)
    {
        var blockUiContainer = new BlockUIContainer();
        renderer.Push(blockUiContainer);
        var expander = new Expander()
        {
            IsExpanded = true,
            Content = obj,
        };
        expander.SetResourceReference(FrameworkElement.StyleProperty, RequestStyleKey);
        renderer.Push(expander);
        renderer.Pop();
        renderer.Pop();
    }
}

