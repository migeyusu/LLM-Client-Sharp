using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;

namespace LLMClient.Render;

public class FunctionCallBlockRenderer : WpfObjectRenderer<FunctionCallBlock>
{
    public static ComponentResourceKey FunctionCallBlockExpanderStyleKey { get; } =
        new(typeof(FunctionCallBlockRenderer), nameof(FunctionCallBlockExpanderStyleKey));

    protected override void Write(WpfRenderer renderer, FunctionCallBlock obj)
    {
        var blockUiContainer = new BlockUIContainer();
        renderer.Push(blockUiContainer);
        var expander = new Expander()
        {
            IsExpanded = false,
        };
        expander.SetResourceReference(FrameworkElement.StyleProperty, FunctionCallBlockExpanderStyleKey);
        renderer.Push(expander);
        expander.Content = obj.Lines.ToString().Trim();
        renderer.Pop();
        renderer.Pop();
    }
}