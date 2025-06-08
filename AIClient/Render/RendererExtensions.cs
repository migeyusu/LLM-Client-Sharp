using System.Windows.Documents;
using Markdig;

namespace LLMClient.Render;

internal static class RendererExtensions
{
    private static readonly CustomRenderer Renderer;

    private static readonly MarkdownPipeline DefaultPipeline =
        new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseThinkBlock()
            .UseGenericAttributes()
            .Build();

    static RendererExtensions()
    {
        Renderer = new CustomRenderer();
        Renderer.Initialize();
        DefaultPipeline.Setup(Renderer);
    }

    public static FlowDocument ToFlowDocument(this string raw)
    {
        return Markdig.Wpf.Markdown.ToFlowDocument(raw, DefaultPipeline, Renderer);
    }


    public static MarkdownPipelineBuilder UseThinkBlock(
        this MarkdownPipelineBuilder pipeline)
    {
        pipeline.Extensions.ReplaceOrAdd<ThinkBlockExtension>((IMarkdownExtension)new ThinkBlockExtension());
        return pipeline;
    }

    internal static string SubstringAtIndexes(this string str, int startIndex, int endIndex)
    {
        return str.Substring(startIndex, endIndex - startIndex);
    }
}