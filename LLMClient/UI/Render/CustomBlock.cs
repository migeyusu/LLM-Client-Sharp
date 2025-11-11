using System.Text;
using Markdig.Parsers;
using Markdig.Syntax;

namespace LLMClient.UI.Render;

public class CustomLeafBlock : LeafBlock
{
    public CustomLeafBlock(BlockParser parser) : base(parser)
    {
    }

    public StringBuilder ContentBuilder { get; set; } = new StringBuilder();

    public string Content
    {
        get { return ContentBuilder.ToString(); }
    }
}
