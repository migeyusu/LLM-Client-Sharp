using Markdig.Parsers;
using Markdig.Syntax;

namespace LLMClient.Render;

public class ThinkBlock : LeafBlock
{
    public ThinkBlock(BlockParser parser) : base(parser) { }
}