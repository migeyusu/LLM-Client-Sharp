using System.Windows.Documents;
using Markdig;

namespace LLMClient.Render;

internal static class RendererExtensions
{
    static RendererExtensions()
    {
    }

    public static FlowDocument RenderOnFlowDocument(this string raw, FlowDocument? result = null)
    {
        result ??= new FlowDocument();
        CustomRenderer.Instance.RenderRaw(raw, result);
        return result;
    }

    //todo: markdig渲染 改进： 1. 支持动态增加obj，每次循环后在原有FlowDocument基础上增加 2. 支持动态增加文本

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

    [Obsolete]
    public static MarkdownPipelineBuilder UseFunctionCallBlock(
        this MarkdownPipelineBuilder pipeline)
    {
        pipeline.Extensions.ReplaceOrAdd<FunctionCallBlockExtension>(new FunctionCallBlockExtension());
        return pipeline;
    }

    [Obsolete]
    public static MarkdownPipelineBuilder UseFunctionResultBlock(
        this MarkdownPipelineBuilder pipeline)
    {
        pipeline.Extensions.ReplaceOrAdd<FunctionResultBlockExtension>(new FunctionResultBlockExtension());
        return pipeline;
    }
}