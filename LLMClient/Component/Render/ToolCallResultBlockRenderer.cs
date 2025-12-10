using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;

namespace LLMClient.Component.Render;

public class ToolCallResultBlockRenderer : WpfObjectRenderer<ToolCallResultBlock>
{
    public static ComponentResourceKey ToolCallResultStyleKey { get; } =
        new(typeof(ToolCallResultBlockRenderer), nameof(ToolCallResultStyleKey));
    
    protected override void Write(WpfRenderer renderer, ToolCallResultBlock obj)
    {
        var blockUiContainer = new BlockUIContainer();
        renderer.Push(blockUiContainer);
        var expander = new Expander()
        {
            IsExpanded = false,
            Content = obj
        };
        expander.SetResourceReference(FrameworkElement.StyleProperty, ToolCallResultStyleKey);
        renderer.Push(expander);
        renderer.Pop();
        renderer.Pop();
    }
}