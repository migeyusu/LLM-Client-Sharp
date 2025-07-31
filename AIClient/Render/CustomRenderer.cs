using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using Markdig;
using Markdig.Renderers;
using Markdig.Wpf;
using Markdown = Markdig.Markdown;

namespace LLMClient.Render;

public class CustomRenderer : WpfRenderer
{
    public static CustomRenderer Instance => Renderer;

    private static readonly CustomRenderer Renderer;

    private static readonly MarkdownPipeline DefaultPipeline =
        new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseThinkBlock()
            /*.UseFunctionCallBlock()
            .UseFunctionResultBlock()*/
            .UseGenericAttributes()
            .Build();

    static CustomRenderer()
    {
        Renderer = new CustomRenderer();
        Renderer.Initialize();
        DefaultPipeline.Setup(Renderer);
    }

    public static CustomRenderer NewRenderer(FlowDocument flowDocument)
    {
        var renderer = new CustomRenderer();
        renderer.Initialize();
        DefaultPipeline.Setup(renderer);
        renderer.LoadDocument(flowDocument);
        return renderer;
    }

    public void RenderItem<T>(T obj, ComponentResourceKey styleKey)
    {
        var expander = new Expander()
        {
            IsExpanded = false,
        };
        expander.SetResourceReference(FrameworkElement.StyleProperty, styleKey);
        expander.Content = obj;
        expander.Header = obj;
        var blockUiContainer = new BlockUIContainer(expander);
        ((IAddChild)Document!).AddChild(blockUiContainer);
    }

    public static ComponentResourceKey FunctionCallStyleKey { get; } =
        new(typeof(CustomRenderer), nameof(FunctionCallStyleKey));

    public static ComponentResourceKey FunctionResultStyleKey { get; } =
        new(typeof(CustomRenderer), (object)nameof(FunctionResultStyleKey));
    
    public static ComponentResourceKey TextReasoningStyleKey { get; } =
        new(typeof(CustomRenderer), (object)nameof(TextReasoningStyleKey));
    
    public static ComponentResourceKey AnnotationStyleKey => new(typeof(CustomRenderer), nameof(AnnotationStyleKey));

    public void RenderRaw(string raw, FlowDocument document)
    {
        if (Document != document)
        {
            LoadDocument(document);
        }

        var markdown = Markdown.Parse(raw, DefaultPipeline);
        this.Render(markdown);
    }

    public void RenderRaw(string raw)
    {
        var markdown = Markdown.Parse(raw, DefaultPipeline);
        this.Render(markdown);
    }

    public override void LoadDocument(FlowDocument document)
    {
        Document = document;
        if (document.ReadLocalValue(FrameworkContentElement.StyleProperty) == DependencyProperty.UnsetValue)
        {
            document.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.DocumentStyleKey);
        }

        this.Push(document);
        LoadRenderers();
    }

    private bool _isRendererLoaded = false;

    protected override void LoadRenderers()
    {
        if (_isRendererLoaded)
        {
            return;
        }

        ObjectRenderers.Add(new TextMateCodeRenderer());
        ObjectRenderers.Add(new LinkInlineRendererEx());
        
        base.LoadRenderers();
        _isRendererLoaded = true;
    }

    public void Initialize()
    {
        LoadRenderers();
    }
}