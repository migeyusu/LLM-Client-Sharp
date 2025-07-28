using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;

namespace LLMClient.Render;

[Obsolete]
public class FunctionCallBlockRenderer : WpfObjectRenderer<FunctionCallBlock>
{
    protected override void Write(WpfRenderer renderer, FunctionCallBlock obj)
    {
        var blockUiContainer = new BlockUIContainer();
        renderer.Push(blockUiContainer);
        var expander = new Expander()
        {
            IsExpanded = false,
        };
        expander.SetResourceReference(FrameworkElement.StyleProperty, CustomRenderer.FunctionCallStyleKey);
        renderer.Push(expander);
        expander.Content = obj.Lines.ToString().Trim();
        renderer.Pop();
        renderer.Pop();
    }
}

