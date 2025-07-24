using System.Text.Json;
using LLMClient.Endpoints.OpenAIAPI;

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
            app.Run(new TestWindow());
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
}