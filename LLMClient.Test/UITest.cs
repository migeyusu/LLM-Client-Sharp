using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.Rag.Document;

namespace LLMClient.Test;

public class UITest
{
    [Fact]
    public void Run()
    {
        var thread = new Thread(o =>
        {
            var app = new App();
            app.InitializeComponent();
            app.Run(new PDFExtractorWindow(new PDFExtractor(@"C:\Users\jie.zhu\Desktop\semantic-kernel.pdf")));
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
    
    [Fact]
    public void SerializeInfo()
    {
        var thread = new Thread(o =>
        {
            var app = new App();
            app.InitializeComponent();
            var apiModelInfo = new APIModelInfo()
            {
                Name = "TestModel",
                Description = "This is a test model.",
                IconUrl = "https://example.com/icon.png",
            };
            var serializeToNode = JsonSerializer.SerializeToNode(apiModelInfo);
            Assert.NotNull(serializeToNode);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void TestImage()
    {
        if (HeadingParser.TryParse("1.5.2.1 Considerations for Using Three Level Topology on a System with More",out var numbering,out string title,out var levels ))
        {
            Debugger.Break();
        }
    }
}

