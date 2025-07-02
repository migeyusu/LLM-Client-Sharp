using Markdig;
using Markdig.Renderers;

namespace LLMClient.Render;

public class FunctionResultBlockExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        pipeline.BlockParsers.Insert(0, new FunctionResultBlockParser());
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer.ObjectRenderers.Find<FunctionResultBlockRenderer>() == null)
        {
            renderer.ObjectRenderers.Insert(0, new FunctionResultBlockRenderer());
        }
    }
}