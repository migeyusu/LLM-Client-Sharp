using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;

namespace LLMClient.Component.Render;

public class ToolCallBlockRenderer : WpfObjectRenderer<ToolCallBlock>
{
    public static ComponentResourceKey ToolCallStyleKey { get; } =
        new ComponentResourceKey(typeof(ToolCallBlockRenderer), (object)nameof(ToolCallStyleKey));

    protected override void Write(WpfRenderer renderer, ToolCallBlock obj)
    {
        var blockUiContainer = new BlockUIContainer();
        renderer.Push(blockUiContainer);
        var expander = new Expander()
        {
            IsExpanded = false,
            Content = obj,
        };
        expander.SetResourceReference(FrameworkElement.StyleProperty, ToolCallStyleKey);
        renderer.Push(expander);
        renderer.Pop();
        renderer.Pop();
    }
}