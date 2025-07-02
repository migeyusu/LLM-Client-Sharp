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
            Margin = new Thickness(0, 5, 0, 5),
        };
        expander.SetResourceReference(FrameworkElement.StyleProperty, FunctionResultBlockExpanderStyleKey);
        renderer.Push(expander);
        expander.Content = obj.Lines.ToString();
        renderer.Pop();
        renderer.Pop();
    }
}