using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;

namespace LLMClient.Render;

public class FunctionResultBlockRenderer : WpfObjectRenderer<FunctionResultBlock>
{
    public static ComponentResourceKey FunctionResultBlockExpanderStyleKey { get; } =
        new(typeof(FunctionResultBlockRenderer), (object)nameof(FunctionResultBlockExpanderStyleKey));

    protected override void Write(WpfRenderer renderer, FunctionResultBlock obj)
    {
        var blockUiContainer = new BlockUIContainer();
        renderer.Push(blockUiContainer);
        var expander = new Expander()
        {
            IsExpanded = false,
        };
        expander.SetResourceReference(FrameworkElement.StyleProperty, FunctionResultBlockExpanderStyleKey);
        renderer.Push(expander);
        expander.Content = obj.Lines.ToString().Trim();
        renderer.Pop();
        renderer.Pop();
    }
}