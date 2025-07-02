using Markdig.Parsers;

namespace LLMClient.Render;

public class FunctionCallBlock : CustomBlock
{
    public FunctionCallBlock(BlockParser parser) : base(parser)
    {
    }
}