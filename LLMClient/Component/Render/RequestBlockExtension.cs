using Markdig;
using Markdig.Renderers;

namespace LLMClient.Component.Render;

public class RequestBlockExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        if (!pipeline.BlockParsers.Contains<RequestBlockParser>())
        {
            pipeline.BlockParsers.Insert(0, new RequestBlockParser());
        }
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer.ObjectRenderers.Find<RequestBlockRenderer>() == null)
        {
            renderer.ObjectRenderers.Insert(0, new RequestBlockRenderer());
        }
    }
}

