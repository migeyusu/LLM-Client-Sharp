using Markdig.Parsers;
using Markdig.Syntax;

namespace LLMClient.Render;

public class FunctionResultBlock : CustomBlock
{
    public FunctionResultBlock(BlockParser parser) : base(parser)
    {
    }
}