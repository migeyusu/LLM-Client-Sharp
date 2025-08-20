using System.Text;
using UglyToad.PdfPig.DocumentLayoutAnalysis;

namespace LLMClient.Rag.Document;

public class PDFPageBlocks
{
    public PDFPageBlocks(IReadOnlyList<TextBlock> blocks, int pageNumber)
    {
        Blocks = blocks;
        PageNumber = pageNumber;
        var paragraphContentBuilder = new StringBuilder();
        foreach (var block in this.Blocks)
        {
            paragraphContentBuilder.AppendLine(block.Text);
        }

        Content = paragraphContentBuilder.ToString();
    }

    public int PageNumber { get; }

    public IReadOnlyList<TextBlock> Blocks { get; }

    public string Content { get; }
}