using Markdig;
using Markdig.Renderers;

namespace LLMClient.Render;

public class ThinkBlockExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        pipeline.BlockParsers.Insert(0, new ThinkBlockParser());
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer.ObjectRenderers.Find<ThinkBlockRenderer>() == null)
        {
            renderer.ObjectRenderers.Insert(0, new ThinkBlockRenderer());
        }
    }
}