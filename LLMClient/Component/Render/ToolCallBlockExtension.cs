using Markdig;
using Markdig.Renderers;

namespace LLMClient.Component.Render;


public class ToolCallBlockExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        pipeline.BlockParsers.Insert(0, new ToolCallBlockParser());
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer.ObjectRenderers.Find<ToolCallBlockRenderer>() == null)
        {
            renderer.ObjectRenderers.Insert(0, new ToolCallBlockRenderer());
        }
    }
}