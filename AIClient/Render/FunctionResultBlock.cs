using Markdig.Parsers;

namespace LLMClient.Render;

[Obsolete]
public class FunctionResultBlock : CustomBlock
{
    public FunctionResultBlock(BlockParser parser) : base(parser)
    {
    }
}