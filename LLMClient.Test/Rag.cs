using System.Windows;
using Xunit.Abstractions;

namespace LLMClient.Test;

public class Rag
{
    private ITestOutputHelper output;

    public Rag(ITestOutputHelper output)
    {
        this.output = output;
    }


    [Fact]
    public async Task PDF()
    {
        string pdfPath =
            @"C:\Users\jie.zhu\Documents\WXWork\1688854281599012\Cache\File\2025-07\AMT_M1A0_Datasheet_v0p5_250428.pdf"; // 替换为实际路径
        var pdfExtractor = new PDFExtractor(pdfPath);
        pdfExtractor.Initialize();
        pdfExtractor.Analyze();
    }
}