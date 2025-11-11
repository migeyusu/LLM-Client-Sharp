using Markdig;
using Markdig.Renderers;

namespace LLMClient.UI.Render;


public class ToolCallResultBlockExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        pipeline.BlockParsers.Insert(0, new ToolCallResultBlockParser());
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer.ObjectRenderers.Find<ToolCallResultBlockRenderer>() == null)
        {
            renderer.ObjectRenderers.Insert(0, new ToolCallResultBlockRenderer());
        }
    }
}