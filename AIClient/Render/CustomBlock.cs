using System.Text;
using Markdig.Parsers;
using Markdig.Syntax;

namespace LLMClient.Render;

public class CustomBlock : LeafBlock
{
    public CustomBlock(BlockParser parser) : base(parser)
    {
    }

    public StringBuilder ContentBuilder { get; set; } = new StringBuilder();

    public string Content
    {
        get { return ContentBuilder.ToString(); }
    }
}