using Markdig.Extensions.Mathematics;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace LLMClient.Component.Render;

public class CustomMathBlockParser : CustomFencedBlockParser<MathBlock>
{
    public string DefaultClass { get; set; }

    public CustomMathBlockParser() : base("\\[", "\\]")
    {
        InfoParser = NoInfoParser;
        DefaultClass = "math";
    }

    protected override MathBlock CreateBlock(BlockProcessor processor)
    {
        var block = new MathBlock(this);
        block.GetAttributes().AddClass(DefaultClass);
        return block;
    }
    
    private static bool NoInfoParser(BlockProcessor state, ref StringSlice line, IFencedBlock fenced,
        char openingCharacter)
    {
        for (int i = line.Start; i <= line.End; i++)
        {
            if (!line.Text[i].IsSpaceOrTab())
            {
                return false;
            }
        }

        return true;
    }
}