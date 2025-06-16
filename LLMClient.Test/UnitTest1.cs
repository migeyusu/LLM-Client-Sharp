using System.Diagnostics;
using System.Net;
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
        var task = Task.Run((async () => await Task.Delay(1)));
        await Task.Delay(10);
        output.WriteLine($"{task.IsCompleted}");
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
}