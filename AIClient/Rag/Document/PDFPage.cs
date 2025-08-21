using System.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis;

namespace LLMClient.Rag.Document;

public class PDFPage
{
    public PDFPage(IReadOnlyList<TextBlock> blocks, int pageNumber, IReadOnlyList<IPdfImage> images)
    {
        Blocks = blocks;
        PageNumber = pageNumber;
        Images = images;
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

    public IReadOnlyList<IPdfImage> Images { get; }
}
