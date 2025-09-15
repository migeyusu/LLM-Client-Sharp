using Markdig.Parsers;

namespace LLMClient.UI.Render;

public class FunctionCallBlock : CustomLeafBlock
{
    public FunctionCallBlock(BlockParser parser) : base(parser)
    {
    }
}