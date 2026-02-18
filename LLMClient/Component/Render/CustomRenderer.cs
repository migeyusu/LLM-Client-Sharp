using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;
using Markdig.Syntax;
using Markdig.Wpf;
using Markdown = Markdig.Markdown;

namespace LLMClient.Component.Render;



public class CustomMarkdownRenderer : WpfRenderer
{
    public static CustomMarkdownRenderer Instance => Renderer;

    private static readonly CustomMarkdownRenderer Renderer;

    public static readonly MarkdownPipeline DefaultPipeline =
        new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseThinkBlock()
            .UseRequestBlock()
            .UseMathematics()
            .UseCustomMathematics()
            .UseFunctionCallBlock()
            .UseFunctionResultBlock()
            .UseGenericAttributes()
            .Build();

    static CustomMarkdownRenderer()
    {
        Renderer = new CustomMarkdownRenderer();
        Renderer.Initialize();
        DefaultPipeline.Setup(Renderer);
    }

    public static ComponentResourceKey PermissionRequestStyleKey =>
        new(typeof(CustomMarkdownRenderer), nameof(PermissionRequestStyleKey));

    public static CustomMarkdownRenderer NewRenderer(FlowDocument flowDocument)
    {
        var renderer = new CustomMarkdownRenderer();
        renderer.Initialize();
        DefaultPipeline.Setup(renderer);
        renderer.LoadDocument(flowDocument);
        return renderer;
    }

    public static ComponentResourceKey FunctionCallStyleKey { get; } =
        new(typeof(CustomMarkdownRenderer), nameof(FunctionCallStyleKey));


    public static ComponentResourceKey FunctionResultStyleKey { get; } =
        new(typeof(CustomMarkdownRenderer), (object)nameof(FunctionResultStyleKey));

    public static ComponentResourceKey AnnotationStyleKey => new(typeof(CustomMarkdownRenderer), nameof(AnnotationStyleKey));

    public void AppendExpanderItem<T>(T obj, ComponentResourceKey styleKey)
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

    public void AppendMarkdownObject(MarkdownObject obj)
    {
        foreach (var renderer in ObjectRenderers)
        {
            if (renderer.Accept(this, obj.GetType()))
            {
                renderer.Write(this, obj);
                break;
            }
        }
    }

    public void RenderRaw(string raw, FlowDocument document)
    {
        if (Document != document)
        {
            LoadDocument(document);
        }

        var markdown = Markdown.Parse(raw, DefaultPipeline);
        this.Render(markdown);
    }


    public FlowDocument? RenderRaw(string raw)
    {
        if (string.IsNullOrEmpty(raw.Trim()))
        {
            return this.Document;
        }

        var markdown = Markdown.Parse(raw, DefaultPipeline);
        return (FlowDocument?)this.Render(markdown);
    }

    /*public async Task RenderRawAsync(string raw)
    {
        if (string.IsNullOrEmpty(raw.Trim()))
        {
            return;
        }

        var markdownDocument = await Task.Run(() => Markdown.Parse(raw, DefaultPipeline));
        this.Render(markdownDocument);
    }*/

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

        ObjectRenderers.RemoveAll((renderer => renderer is ListRenderer));
        ObjectRenderers.Add(new SafeListRender());
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