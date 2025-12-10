using Markdig;
using Markdig.Renderers;

namespace LLMClient.Component.Render;

public class ThinkBlockExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        //do nothing, the parser is registered in CustomPipeline
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