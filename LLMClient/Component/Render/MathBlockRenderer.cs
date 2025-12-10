using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using LLMClient.Component.CustomControl;
using Markdig.Extensions.Mathematics;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;

namespace LLMClient.Component.Render;

public class MathBlockRenderer : WpfObjectRenderer<MathBlock>
{
    public static ComponentResourceKey MathBlockStyleKey { get; } =
        new(typeof(ToolCallBlockRenderer), nameof(MathBlockStyleKey));

    protected override void Write(WpfRenderer renderer, MathBlock obj)
    {
        var blockUiContainer = new BlockUIContainer();
        renderer.Push(blockUiContainer);
        var formula = obj.Lines.ToString();
        var asyncThemedIcon = new AsyncThemedIcon(async () => await FormulaRenderCache.Instance.TryGet(formula));
        var contentControl = new ContentControl()
        {
            Content = new MathBlockContext(obj, asyncThemedIcon),
        };
        contentControl.SetResourceReference(FrameworkElement.StyleProperty, MathBlockStyleKey);
        renderer.Push(contentControl);
        renderer.Pop();
        renderer.Pop();
    }
}

public class MathBlockInlineRenderer : WpfObjectRenderer<MathInline>
{
    public static ComponentResourceKey MathInlineStyleKey { get; } =
        new(typeof(ToolCallBlockRenderer), nameof(MathInlineStyleKey));

    protected override void Write(WpfRenderer renderer, MathInline obj)
    {
        var inlineUiContainer = new InlineUIContainer();
        renderer.Push(inlineUiContainer);
        var formula = obj.Content.ToString();
        var asyncThemedIcon = new AsyncThemedIcon(async () => await FormulaRenderCache.Instance.TryGet(formula));
        var contentControl = new ContentControl()
        {
            Content = new MathInlineContext(obj, asyncThemedIcon),
        };
        contentControl.SetResourceReference(FrameworkElement.StyleProperty, MathInlineStyleKey);
        renderer.Push(contentControl);
        renderer.Pop();
        renderer.Pop();
    }
}

public class MathInlineContext
{
    public AsyncThemedIcon Image { get; }

    public string Latex { get; }
    
    private readonly MathInline _mathInline;

    public MathInlineContext(MathInline mathInline, AsyncThemedIcon asyncThemedIcon)
    {
        this._mathInline = mathInline;
        Image = asyncThemedIcon;
        Latex = mathInline.Content.ToString();
    }
}