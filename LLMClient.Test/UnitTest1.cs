using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using LLMClient.UI.MCP.Servers;
using Xunit.Abstractions;

namespace LLMClient.Test;

//dotnet publish .\LLMClient.csproj -p:PublishProfile=FolderProfile
public class UnitTest1
{
    private ITestOutputHelper output;

    public UnitTest1(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async void Test1()
    {
        var fileSystemPlugin = new FileSystemPlugin(new []{"D:\\Dev\\LLM-Client-Sharp\\AIClient\\bin\\Release\\net8.0-windows\\publish\\win-x64\\Dialogs"});
        var listAllowedDirectories = fileSystemPlugin.ListAllowedDirectories();
        output.WriteLine($"Listing allowed directories:{listAllowedDirectories}");
    }
    


    [Fact]
    public async void GetGithubModels()
    {
        HttpClientHandler handler = new HttpClientHandler()
            { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
        using (var httpClient = new HttpClient(handler))
        {
            using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://xiaoai.plus/api/pricing"))
            {
                httpRequestMessage.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
                using (var message = await httpClient.SendAsync(httpRequestMessage))
                {
                    message.EnsureSuccessStatusCode();
                    var content = await message.Content.ReadAsStringAsync();
                    output.WriteLine(content);
                }
            }
        }
    }

    [Fact]
    public async void UrlCheck()
    {
        using (var httpClient = new HttpClient())
        {
            using (var message = await httpClient.GetAsync("https://data.ocoolai.com/items/models?limit=1000"))
            {
                message.EnsureSuccessStatusCode();
                var readAsStringAsync = await message.Content.ReadAsStringAsync();
                output.WriteLine(readAsStringAsync);
            }
        }
    }

    [Fact]
    public async void TestImageReqeust()
    {
        var url =
            "https://t0.gstatic.com/faviconV2?client=SOCIAL&type=FAVICON&fallback_opts=TYPE,SIZE,URL&url=https://chutes.ai/&size=256";
        var extension = Path.GetExtension(url);
        using (var cancellationTokenSource = new CancellationTokenSource(5000))
        {
            var cancellationToken = cancellationTokenSource.Token;
            using (var message = await new HttpClient().GetAsync(url, cancellationToken))
            {
                message.EnsureSuccessStatusCode();
                if (string.IsNullOrEmpty(extension))
                {
                    var mediaType = message.Content.Headers.ContentType?.MediaType;
                    if (!string.IsNullOrEmpty(mediaType))
                    {
                        Debugger.Break();
                    }
                }

                if (string.IsNullOrEmpty(extension))
                {
                    Debugger.Break();
                }
                
            }
        }
    }
}