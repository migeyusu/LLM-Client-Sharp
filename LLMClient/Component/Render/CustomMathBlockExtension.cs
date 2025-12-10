using Markdig;
using Markdig.Renderers;

namespace LLMClient.Component.Render;

public class CustomMathBlockExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        if (!pipeline.InlineParsers.Contains<LatexMathInlineParser>())
        {
            pipeline.InlineParsers.Insert(0, new LatexMathInlineParser());
        }

        if (!pipeline.BlockParsers.Contains<CustomMathBlockParser>())
        {
            pipeline.BlockParsers.Insert(0, new CustomMathBlockParser());
        }
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        renderer.ObjectRenderers.Insert(0, new MathBlockRenderer());
        renderer.ObjectRenderers.Insert(0, new MathBlockInlineRenderer());
    }
}