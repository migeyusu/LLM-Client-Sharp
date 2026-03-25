using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using Markdig;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;
using Markdig.Syntax;
using Markdig.Wpf;
using Markdown = Markdig.Markdown;

namespace LLMClient.Component.Render;

public class CustomMarkdownRenderer : WpfRenderer
{
    // 新增：控制是否启用 TextMate 高亮
    public bool EnableTextMateHighlighting { get; set; } = true;

    public bool EditMode { get; set; } = false;

    public static CustomMarkdownRenderer DefaultInstance { get; } = CreateDefaultRenderer();

    public static MarkdownPipelineBuilder DefaultBuilder { get; } = CreateDefaultPipelineBuilder();

    public static MarkdownPipeline DefaultPipeline { get; } = DefaultBuilder.Build();

    public static CustomMarkdownRenderer CreateDefaultRenderer()
    {
        var defaultPipeline = CreateDefaultPipelineBuilder().Build();
        var renderer = new CustomMarkdownRenderer() { Pipeline = defaultPipeline };
        renderer.Initialize();
        defaultPipeline.Setup(renderer);
        return renderer;
    }

    public static MarkdownPipelineBuilder CreateDefaultPipelineBuilder()
    {
        return new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseThinkBlock()
            .UseRequestBlock()
            .UseMathematics()
            .UseCustomMathematics()
            .UseFunctionCallBlock()
            .UseFunctionResultBlock()
            .UseGenericAttributes();
    }

    private static readonly Lazy<MarkdownPipeline> EditModePipelineLazy = new(() =>
    {
        // 编辑模式下的 MarkdownPipeline，禁用除代码块以外的所有解析器
        var builder = new MarkdownPipelineBuilder();
        builder.BlockParsers.Clear();
        builder.InlineParsers.Clear();
        builder.BlockParsers.Add(new FencedCodeBlockParser());
        builder.BlockParsers.Add(new ParagraphBlockParser());
        builder.UseGenericAttributes();
        return builder.Build();
    });

    public static readonly MarkdownPipeline EditModePipeline = EditModePipelineLazy.Value;

    public static CustomMarkdownRenderer EditRenderer(FlowDocument flowDocument)
    {
        var renderer = new CustomMarkdownRenderer() { Pipeline = EditModePipeline };
        renderer.Initialize();
        EditModePipeline.Setup(renderer);
        renderer.LoadDocument(flowDocument);
        renderer.EditMode = true;
        return renderer;
    }


    public static CustomMarkdownRenderer NewRenderer(FlowDocument flowDocument, bool? enableTextMate = null,
        bool? editMode = null)
    {
        var renderer = CreateDefaultRenderer();
        renderer.LoadDocument(flowDocument);
        if (enableTextMate.HasValue)
            renderer.EnableTextMateHighlighting = enableTextMate.Value;
        if (editMode.HasValue)
            renderer.EditMode = editMode.Value;
        return renderer;
    }

    public static ComponentResourceKey FunctionCallStyleKey { get; } =
        new(typeof(CustomMarkdownRenderer), nameof(FunctionCallStyleKey));

    public static ComponentResourceKey FunctionResultStyleKey { get; } =
        new(typeof(CustomMarkdownRenderer), (object)nameof(FunctionResultStyleKey));

    public static ComponentResourceKey AnnotationStyleKey =>
        new(typeof(CustomMarkdownRenderer), nameof(AnnotationStyleKey));

    public required MarkdownPipeline Pipeline { get; init; }

    public void InsertExpanderItem<T>(T obj, ComponentResourceKey styleKey)
    {
        var expander = new Expander()
        {
            IsExpanded = false,
        };
        expander.SetResourceReference(FrameworkElement.StyleProperty, styleKey);
        expander.Content = obj;
        expander.Header = obj;
        var blockUiContainer = new BlockUIContainer(expander);
        if (Document == null)
        {
            return;
        }

        if (Document.Blocks.FirstBlock == null)
        {
            Document.Blocks.Add(blockUiContainer);
            return;
        }

        Document!.Blocks.InsertBefore(Document.Blocks.FirstBlock, blockUiContainer);
    }

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

        var markdown = Markdown.Parse(raw, this.Pipeline);
        this.Render(markdown);
    }


    public async Task RenderMarkdown(string raw)
    {
        if (string.IsNullOrEmpty(raw.Trim()))
        {
            return;
        }

        var markdown = await Task.Run(() => Markdown.Parse(raw, this.Pipeline));
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