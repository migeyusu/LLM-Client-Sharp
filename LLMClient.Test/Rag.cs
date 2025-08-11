using System.Windows;
using LLMClient.Rag;
using LLMClient.Rag.Document;
using Xunit.Abstractions;

namespace LLMClient.Test;

public class Rag
{
    private ITestOutputHelper output;
    const string pdfPath =
         @"C:\Users\jie.zhu\Documents\WXWork\1688854281599012\Cache\File\2025-07\AMT_M1A0_Datasheet_v0p5_250428.pdf";
    public Rag(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void PDFClip()
    {
        var thread = new Thread(o =>
        {
            var app = new App();
            app.InitializeComponent();
            app.Run(new PDFExtractorWindow(new PDFExtractor(pdfPath)));
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public async Task PDFExtractor()
    {
        var pdfExtractor = new PDFExtractor(pdfPath);
        pdfExtractor.Initialize();
        pdfExtractor.Analyze();
    }

    [Fact]
    public async Task PDFEmbedding()
    {
        var pdfExtractor = new PDFExtractor(pdfPath);
        pdfExtractor.Initialize(new Thickness(10, 55, 10, 62));
        var contentNodes = pdfExtractor.Analyze();
        var docChunks =
            await contentNodes.ToSKDocChunks("doc1", Task<string>.FromResult, CancellationToken.None);
    }

    [Fact]
    public async Task DataStore()
    {
        var semanticKernelStore = new SemanticKernelStore("Data Source=mydatabase.db");
    }
}