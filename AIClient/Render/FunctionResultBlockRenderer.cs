using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;

namespace LLMClient.Render;

[Obsolete]
public class FunctionResultBlockRenderer : WpfObjectRenderer<FunctionResultBlock>
{
    protected override void Write(WpfRenderer renderer, FunctionResultBlock obj)
    {
        var blockUiContainer = new BlockUIContainer();
        renderer.Push(blockUiContainer);
        var expander = new Expander()
        {
            IsExpanded = false,
        };
        expander.SetResourceReference(FrameworkElement.StyleProperty, CustomRenderer.FunctionResultStyleKey);
        renderer.Push(expander);
        expander.Content = obj.Lines.ToString().Trim('\n', '\r');
        renderer.Pop();
        renderer.Pop();
    }
}