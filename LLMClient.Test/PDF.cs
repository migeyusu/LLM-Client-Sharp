using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.Export;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using Xunit.Abstractions;

namespace LLMClient.Test;

public class PDF
{
    private ITestOutputHelper _output;

    public PDF(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public void TestExtract()
    {
        const string pdfPath =
            @"C:\Users\jie.zhu\Documents\WXWork\1688854281599012\Cache\File\2025-07\AMT_M1A0_Datasheet_v0p5_250428.pdf";
        var pageXmlTextExporter =
            new PageXmlTextExporter(NearestNeighbourWordExtractor.Instance, DocstrumBoundingBoxes.Instance);
        using (var pdfDocument = PdfDocument.Open(pdfPath))
        {
            var s = pageXmlTextExporter.Get(pdfDocument.GetPage(10));
        }
        
    }
}