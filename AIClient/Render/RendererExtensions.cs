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
            .UseFunctionCallBlock()
            .UseFunctionResultBlock()
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

    internal static string SubstringAtIndexes(this string str, int startIndex, int endIndex)
    {
        return str.Substring(startIndex, endIndex - startIndex);
    }

    public static MarkdownPipelineBuilder UseThinkBlock(
        this MarkdownPipelineBuilder pipeline)
    {
        pipeline.Extensions.ReplaceOrAdd<ThinkBlockExtension>(new ThinkBlockExtension());
        return pipeline;
    }

    public static MarkdownPipelineBuilder UseFunctionCallBlock(
        this MarkdownPipelineBuilder pipeline)
    {
        pipeline.Extensions.ReplaceOrAdd<FunctionCallBlockExtension>(new FunctionCallBlockExtension());
        return pipeline;
    }

    public static MarkdownPipelineBuilder UseFunctionResultBlock(
        this MarkdownPipelineBuilder pipeline)
    {
        pipeline.Extensions.ReplaceOrAdd<FunctionResultBlockExtension>(new FunctionResultBlockExtension());
        return pipeline;
    }
}