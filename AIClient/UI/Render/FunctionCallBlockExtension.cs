using Markdig;
using Markdig.Renderers;

namespace LLMClient.UI.Render;

[Obsolete]
public class FunctionCallBlockExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        pipeline.BlockParsers.Insert(0, new FunctionCallBlockParser());
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer.ObjectRenderers.Find<FunctionCallBlockRenderer>() == null)
        {
            renderer.ObjectRenderers.Insert(0, new FunctionCallBlockRenderer());
        }
    }
}